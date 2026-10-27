using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusHR.BuildingBlocks.Hosting;
using NexusHR.Onboarding.Api.Controllers;
using NexusHR.Onboarding.Application;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Tests;

public sealed class OnboardingApiTests : IDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;
    private readonly MemoryRepository repository = new();
    private readonly RecordingPublisher events = new();

    public OnboardingApiTests()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.AddApiDefaults();
        builder.Services.AddControllers().AddApplicationPart(typeof(OnboardingsController).Assembly);
        builder.Services.AddApplication();
        builder.Services.AddSingleton<IEmployeeOnboardingRepository>(repository);
        builder.Services.AddSingleton<IUnitOfWork>(repository);
        builder.Services.AddSingleton<IIntegrationEventPublisher>(events);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, IdentityHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        app = builder.Build();
        app.UseApiDefaults();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Start();
        client = app.GetTestClient();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Auditor", HttpStatusCode.Forbidden)]
    [InlineData("HrManager", HttpStatusCode.Forbidden)]
    [InlineData("DepartmentManager", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.Forbidden)]
    [InlineData("FinanceSpecialist", HttpStatusCode.Forbidden)]
    public async Task TaskMutationRejectsUnauthorizedRoles(string? role, HttpStatusCode expected)
    {
        if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.PutAsJsonAsync(TaskUrl(), new { status = 2 });
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(0, repository.Saves);
    }

    [Theory]
    [InlineData("ItSpecialist")]
    [InlineData("HrSpecialist")]
    public async Task EditorsCanTrackTasksAndOnlyHrCanComplete(string role)
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        foreach (var task in repository.Item.Tasks)
        {
            var url = $"/api/onboardings/{repository.Item.Id}/tasks/{task.Id}/status";
            using var started = await client.PutAsJsonAsync(url, new { status = 2 });
            Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
            using var completed = await client.PutAsJsonAsync(url, new { status = 3 });
            Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        }
        using var response = await client.PostAsync($"/api/onboardings/{repository.Item.Id}/complete", null);
        Assert.Equal(role == "HrSpecialist" ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(role == "HrSpecialist" ? 1 : 0, events.Messages.Count);
        if (role == "HrSpecialist")
        {
            Assert.IsType<NexusHR.Contracts.Onboarding.EmployeeOnboardingCompletedIntegrationEvent>(events.Messages.Single());
            using var repeated = await client.PostAsync($"/api/onboardings/{repository.Item.Id}/complete", null);
            Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
            Assert.Single(events.Messages);
        }
    }

    [Fact]
    public async Task InvalidStateValidationAndMissingRecordProduceProblemDetails()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "HrSpecialist");
        using var invalid = await client.PutAsJsonAsync(TaskUrl(), new { status = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        using var premature = await client.PostAsync($"/api/onboardings/{repository.Item.Id}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        using var missing = await client.PostAsync($"/api/onboardings/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(0, repository.Saves);
    }

    private string TaskUrl() => $"/api/onboardings/{repository.Item.Id}/tasks/{repository.Item.Tasks.First().Id}/status";
    public void Dispose() { client.Dispose(); app.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

    private sealed class MemoryRepository : IEmployeeOnboardingRepository, IUnitOfWork
    {
        public EmployeeOnboarding Item { get; } = OnboardingWorkflowTests.Create();
        public int Saves { get; private set; }
        public Task<EmployeeOnboarding?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == Item.Id ? Item : null);
        public Task<EmployeeOnboarding?> GetByHiringProcessIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == Item.HiringProcessId ? Item : null);
        public Task<IReadOnlyCollection<EmployeeOnboarding>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<EmployeeOnboarding>>([Item]);
        public Task AddAsync(EmployeeOnboarding item, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return Task.FromResult(1); }
    }

    private sealed class IdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class RecordingPublisher : IIntegrationEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class
        { Messages.Add(message); return Task.CompletedTask; }
    }
}
