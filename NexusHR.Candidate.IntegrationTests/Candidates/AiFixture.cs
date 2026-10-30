using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusHR.BuildingBlocks.Hosting;
using NexusHR.Candidate.Api.Controllers;
using NexusHR.Candidate.Application;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Infrastructure;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

// All credentials, fake identity and fake provider remain in this test assembly.
public sealed class AiFixture : IAsyncLifetime
{
    public readonly PostgreSqlContainer Postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    public readonly IContainer Broker = new ContainerBuilder("rabbitmq:4.3.3-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", "test-user").WithEnvironment("RABBITMQ_DEFAULT_PASS", "test-password")
        .WithPortBinding(5672, true).WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672)).Build();
    public readonly IContainer Minio = new ContainerBuilder("nexushr-minio:2025-10-15")
        .WithEnvironment("MINIO_ROOT_USER", "test-access").WithEnvironment("MINIO_ROOT_PASSWORD", "test-storage-password")
        .WithCommand("server", "/data").WithPortBinding(9000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x => x.ForPort(9000).ForPath("/minio/health/live"))).Build();
    public WebApplication App = null!;
    public FakeCvProvider Provider = new();
    public CvAnalysisOptions Options => App.Services.GetRequiredService<CvAnalysisOptions>();
    public IServiceScopeFactory Scopes => App.Services.GetRequiredService<IServiceScopeFactory>();
    public IReadOnlyDictionary<string, string?>? ExtraConfiguration { get; init; }
    public Action<IServiceCollection>? ExtraServices { get; init; }
    public async Task InitializeAsync()
    {
        await Task.WhenAll(Postgres.StartAsync(), Broker.StartAsync(), Minio.StartAsync());
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CandidateDatabase"] = Postgres.GetConnectionString(),
            ["Minio:Endpoint"] = $"{Minio.Hostname}:{Minio.GetMappedPublicPort(9000)}", ["Minio:AccessKey"] = "test-access",
            ["Minio:SecretKey"] = "test-storage-password", ["Minio:BucketName"] = "ai-test-cvs",
            ["RabbitMq:Host"] = Broker.Hostname, ["RabbitMq:Port"] = Broker.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "test-user", ["RabbitMq:Password"] = "test-password",
            ["CvAnalysis:Enabled"] = "true", ["CvAnalysis:Model"] = "synthetic-test-model", ["CvAnalysis:PriceVersion"] = "test-rates-v1",
            ["CvAnalysis:InputUsdPerMillion"] = "1", ["CvAnalysis:OutputUsdPerMillion"] = "2", ["CvAnalysis:RequestsPerUserPerDay"] = "100",
        });
        if (ExtraConfiguration is not null) builder.Configuration.AddInMemoryCollection(ExtraConfiguration);
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        builder.Services.Replace(ServiceDescriptor.Singleton<ICvAnalysisProvider>(Provider));
        var worker = builder.Services.Single(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(CvAnalysisWorker));
        builder.Services.Remove(worker); // Tests control worker scheduling/crash boundaries deterministically.
        ExtraServices?.Invoke(builder.Services);
        builder.Services.AddControllers().AddApplicationPart(typeof(CvAnalysesController).Assembly);
        builder.Services.AddScoped<CvAnalysisExceptionFilter>();
        builder.Services.AddAuthentication("AiTest").AddScheme<AuthenticationSchemeOptions, Identity>("AiTest", _ => { });
        builder.Services.AddAuthorization(); builder.AddApiDefaults();
        App = builder.Build(); App.UseApiDefaults(); App.UseAuthentication(); App.UseAuthorization(); App.MapControllers();
        await using (var scope = Scopes.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<CandidateDbContext>().Database.MigrateAsync();
        await App.StartAsync();
    }
    public HttpClient Client(string? roles = "HrSpecialist", string user = "test-hr")
    {
        var client = new HttpClient { BaseAddress = new Uri(App.Urls.Single()) };
        if (roles is not null) { client.DefaultRequestHeaders.Add("X-Ai-Test-User", user); client.DefaultRequestHeaders.Add("X-Ai-Test-Roles", roles); }
        return client;
    }
    public async Task<(Guid Candidate, Guid Document)> SeedAsync(byte[]? content = null, string firstName = "Synthetic", string lastName = "Person")
    {
        content ??= Pdf;
        await using var scope = Scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var candidate = new Domain.Candidates.Candidate(Guid.NewGuid(), firstName, lastName, $"{Guid.NewGuid():N}@example.test", "05555555555");
        var doc = new CandidateDocument(Guid.NewGuid(), candidate.Id, CandidateDocumentType.Cv, "synthetic.pdf", $"test/{Guid.NewGuid():N}.pdf", "application/pdf", content.Length);
        db.Add(candidate); db.Add(doc); await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>().UploadAsync(doc.ObjectName, content, "application/pdf", default);
        return (candidate.Id, doc.Id);
    }
    public async Task WaitAsync(Func<CandidateDbContext, Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (true)
        {
            await using var scope = Scopes.CreateAsyncScope();
            if (await condition(scope.ServiceProvider.GetRequiredService<CandidateDbContext>())) return;
            await Task.Delay(200, timeout.Token);
        }
    }
    public async Task DisposeAsync()
    {
        if (App is not null) { await App.StopAsync(); await App.DisposeAsync(); }
        await Minio.DisposeAsync(); await Broker.DisposeAsync(); await Postgres.DisposeAsync();
    }
    internal static byte[] Pdf
    {
        get
        {
            var text = "BT /F1 12 Tf 50 750 Td (Synthetic Person) Tj 0 -20 Td (C# PostgreSQL RabbitMQ) Tj 0 -20 Td (synthetic@example.test) Tj ET";
            var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {text.Length} >>\nstream\n{text}\nendstream" };
            var pdf = "%PDF-1.4\n"; var offsets = new List<int>();
            for (var i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf += $"{i + 1} 0 obj\n{objects[i]}\nendobj\n"; }
            var xref = pdf.Length; pdf += "xref\n0 6\n0000000000 65535 f \n";
            pdf += string.Concat(offsets.Select(x => $"{x:0000000000} 00000 n \n"));
            pdf += $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"; return Encoding.ASCII.GetBytes(pdf);
        }
    }
    internal static byte[] ContactOnlyPdf => MinimalPdf(
        "BT /F1 12 Tf 50 750 Td (Ada Lovelace) Tj 0 -20 Td (synthetic-demo-000@example.test) Tj 0 -20 Td (05555555555) Tj ET");
    internal static byte[] ImageOnlyPdf => MinimalPdf("q 100 0 0 100 50 600 cm /Im1 Do Q", true);
    internal static byte[] ProfessionalPdf => MinimalPdf(
        "BT /F1 12 Tf 50 750 Td (Synthetic Person) Tj 0 -20 Td (Acme Engineer 2020-01 2022-12) Tj 0 -20 Td (Example University Computer Science BSc 2015-2019) Tj 0 -20 Td (C# PostgreSQL RabbitMQ) Tj 0 -20 Td (English B2) Tj ET");
    private static byte[] MinimalPdf(string text, bool image = false)
    {
        var resource = image ? "/XObject << /Im1 4 0 R >>" : "/Font << /F1 4 0 R >>";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << {resource} >> /Contents 5 0 R >>",
            image ? "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 7 >>\nstream\n000000>\nendstream" :
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {text.Length} >>\nstream\n{text}\nendstream" };
        var pdf = "%PDF-1.4\n"; var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf += $"{i + 1} 0 obj\n{objects[i]}\nendobj\n"; }
        var xref = pdf.Length; pdf += "xref\n0 6\n0000000000 65535 f \n";
        pdf += string.Concat(offsets.Select(x => $"{x:0000000000} 00000 n \n"));
        return Encoding.ASCII.GetBytes(pdf + $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
    }
    private sealed class Identity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Ai-Test-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()) };
            claims.AddRange(Request.Headers["X-Ai-Test-Roles"].ToString().Split(',').Select(x => new Claim(ClaimTypes.Role, x)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}

public sealed class FakeCvProvider : ICvAnalysisProvider
{
    public bool RequiresExternalProcessing => false;
    public int Calls;
    public string? UnavailableCode;
    public Task<string?> GetUnavailableCodeAsync(string model, CancellationToken ct) => Task.FromResult(UnavailableCode);
    public Func<IReadOnlyList<CvTextPage>, CancellationToken, Task<CvProviderResponse>>? Override;
    public Task<CvProviderResponse> AnalyzeAsync(IReadOnlyList<CvTextPage> pages, string model, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        if (Override is not null) return Override(pages, ct);
        var page = pages.First(x => x.Text.Contains("C#", StringComparison.Ordinal));
        return Task.FromResult(new CvProviderResponse(new([new("skill", "C#", page.Number, page.Text.IndexOf("C#", StringComparison.Ordinal), "C#")], []), "synthetic-response", 100, 50));
    }
}
