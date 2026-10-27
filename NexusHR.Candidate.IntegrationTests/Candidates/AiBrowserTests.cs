using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class AiBrowserTests(AiFixture fixture) : IClassFixture<AiFixture>
{
    [AiBrowserFact]
    public async Task RealBrowserRunsAnalysisAndReviewAgainstFakeProviderWithPostgresRabbitMqAndMinio()
    {
        var (candidate, _) = await fixture.SeedAsync();
        using var worker = new CvAnalysisWorker(fixture.Scopes, fixture.Options, NullLogger<CvAnalysisWorker>.Instance);
        await worker.StartAsync(default);
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "NexusHR.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            var process = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root.FullName, "NexusHR.Web"), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "node_modules/@playwright/test/cli.js", "test", "ai-cv-analysis.spec.ts", "--grep", "fake-provider" }) process.ArgumentList.Add(arg);
            process.Environment["E2E_AI_BACKEND_URL"] = fixture.App.Urls.Single();
            process.Environment["E2E_AI_CANDIDATE_ID"] = candidate.ToString();
            using var child = Process.Start(process)!;
            var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try { await child.WaitForExitAsync(timeout.Token); }
            catch { child.Kill(entireProcessTree: true); throw; }
            Assert.True(child.ExitCode == 0, (await stdout) + (await stderr));
            Assert.Equal(1, fixture.Provider.Calls);
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
