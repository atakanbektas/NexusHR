using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using NexusHR.BuildingBlocks.Security;
using NexusHR.Candidate.Api.Controllers;
using NexusHR.Candidate.Application;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class CvUploadAuthorizationTests : IDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;

    public CvUploadAuthorizationTests()
    {
        // Host the real controller and authorization middleware without production
        // startup migrations or external services. Identity is supplied only in tests.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        var services = builder.Services;
        services.AddControllers()
                    .AddApplicationPart(typeof(CandidatesController).Assembly);
                services.AddApplication();
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        "Test", _ => { });
        services.AddAuthorization();
        app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Start();
        client = app.GetTestClient();
    }

    [Fact]
    public async Task Upload_WithoutIdentity_ReturnsUnauthorized()
    {
        using var response = await UploadAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(NexusHrRoles.HrManager)]
    [InlineData(NexusHrRoles.DepartmentManager)]
    [InlineData(NexusHrRoles.ItSpecialist)]
    [InlineData(NexusHrRoles.FinanceSpecialist)]
    [InlineData(NexusHrRoles.Auditor)]
    [InlineData(NexusHrRoles.Admin)]
    [InlineData("UnknownRole")]
    [InlineData("HrManager,Auditor")]
    public async Task Upload_WithoutEditorRole_ReturnsForbidden(string roles)
    {
        client.DefaultRequestHeaders.Add("X-Test-Identity", "user");
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        }

        using var response = await UploadAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(NexusHrRoles.HrSpecialist)]
    [InlineData("Auditor,HrSpecialist")]
    public async Task Upload_WithEditorRole_ReachesFileValidation(string roles)
    {
        client.DefaultRequestHeaders.Add("X-Test-Identity", "user");
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);

        using var response = await UploadAsync();

        // No file is intentional: an authorized caller reaches the real action,
        // without invoking storage or persistence. This is not an upload success test.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Candidate.CvFileRequired",
            await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> UploadAsync()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("validation"), "description");
        return await client.PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/documents/cv", content);
    }

    public void Dispose()
    {
        client.Dispose();
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Identity"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user") };
            foreach (var role in Request.Headers["X-Test-Roles"].ToString()
                         .Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
