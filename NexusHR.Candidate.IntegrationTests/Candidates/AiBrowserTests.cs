using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class AiBrowserTests(AiFixture fixture) : IClassFixture<AiFixture>
{
    [AiBrowserFact]
    public async Task RealBrowserRunsAnalysisAndReviewAgainstFakeProviderWithPostgresRabbitMqAndMinio()
    {
        var (candidate, _) = await fixture.SeedAsync();
        var before = fixture.Provider.Calls;
        await RunBrowserAsync(candidate, "ai-cv-analysis.spec.ts", "fake-provider");
        Assert.Equal(before + 1, fixture.Provider.Calls);
    }

    [AiBrowserFact]
    public async Task ContactOnlyAndLegacyEmptyResultsAreNotShownAsUsefulSuccess()
    {
        var (candidate, document) = await fixture.SeedAsync(AiFixture.ContactOnlyPdf, "Ada", "Lovelace");
        var legacy = new CvAnalysis(candidate, document, "browser-hr", Guid.NewGuid().ToString(), fixture.Options.Model, "OpenAI", 0, DateTime.UtcNow.AddDays(-1));
        var lease = legacy.Claim(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        legacy.Finish(lease, CvAnalysisStatus.Succeeded, "{\"facts\":[],\"warnings\":[\"No content provided for PAGE 1\"]}", null, DateTime.UtcNow);
        await using (var scope = fixture.Scopes.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>(); db.Add(legacy); await db.SaveChangesAsync(); }
        var before = fixture.Provider.Calls;
        await RunBrowserAsync(candidate, "ai-empty-analysis.spec.ts", "fake-provider-empty", "contact");
        Assert.Equal(before, fixture.Provider.Calls);
    }

    [AiBrowserFact]
    public async Task EmptyProviderReplyToProfessionalCvIsShownAsNoFindings()
    {
        var (candidate, _) = await fixture.SeedAsync(); var before = fixture.Provider.Calls;
        fixture.Provider.Override = (_, _) => Task.FromResult(new CvProviderResponse(new([], ["No facts found"]), "empty-browser", 100, 50));
        try { await RunBrowserAsync(candidate, "ai-empty-analysis.spec.ts", "fake-provider-empty", "provider"); }
        finally { fixture.Provider.Override = null; }
        Assert.Equal(before + 1, fixture.Provider.Calls);
    }

    [AiBrowserFact]
    public async Task ImageOnlyPdfShowsTurkishOcrExplanation()
    {
        var (candidate, _) = await fixture.SeedAsync(AiFixture.ImageOnlyPdf); var before = fixture.Provider.Calls;
        await RunBrowserAsync(candidate, "ai-empty-analysis.spec.ts", "fake-provider-empty", "image");
        Assert.Equal(before, fixture.Provider.Calls);
    }

    private async Task RunBrowserAsync(Guid candidate, string file, string filter, string kind = "")
    {
        using var worker = new CvAnalysisWorker(fixture.Scopes, fixture.Options, NullLogger<CvAnalysisWorker>.Instance);
        await worker.StartAsync(default);
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "NexusHR.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            var process = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root.FullName, "NexusHR.Web"), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "node_modules/@playwright/test/cli.js", "test", file, "--grep", filter }) process.ArgumentList.Add(arg);
            process.Environment["E2E_AI_BACKEND_URL"] = fixture.App.Urls.Single();
            process.Environment["E2E_AI_CANDIDATE_ID"] = candidate.ToString();
            process.Environment["E2E_EMPTY_KIND"] = kind;
            using var child = Process.Start(process)!;
            var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try { await child.WaitForExitAsync(timeout.Token); }
            catch { child.Kill(entireProcessTree: true); throw; }
            Assert.True(child.ExitCode == 0, (await stdout) + (await stderr));
        }
        finally { await worker.StopAsync(default); }
    }
}
public sealed class AiBrowserFactAttribute : FactAttribute
{
    public AiBrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_AI_BROWSER_TESTS") != "1")
            Skip = "Run after npm ci, Playwright browser installation and full-stack startup with RUN_AI_BROWSER_TESTS=1 (CI has a dedicated step).";
    }
}
