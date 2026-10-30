using System.Diagnostics;
using System.Net.Http.Json;
using NexusHR.Candidate.Application.CvAnalyses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class AiBrowserTests(AiFixture fixture) : IClassFixture<AiFixture>
{
    [AiBrowserFact]
    public async Task BrowserReviewsStructuredFactsAndReplacesCvWithoutLosingOriginalAnalysis()
    {
        using var client = fixture.Client();
        var created = await client.PostAsJsonAsync("/api/candidates", new { firstName = "Synthetic", lastName = "Person", email = $"portfolio-{Guid.NewGuid():N}@example.test", phoneNumber = "05555555555" });
        created.EnsureSuccessStatusCode(); var candidate = (await created.Content.ReadFromJsonAsync<CreatedCandidate>())!.Id;
        using var body = new MultipartFormDataContent(); var file = new ByteArrayContent(AiFixture.ProfessionalPdf);
        file.Headers.ContentType = new("application/pdf"); body.Add(file, "file", "synthetic-professional.pdf");
        (await client.PostAsync($"/api/candidates/{candidate}/documents/cv", body)).EnsureSuccessStatusCode();
        fixture.Provider.Override = (pages, _) => {
            var page = pages[0];
            CvFact Fact(string category, string value, string quote, params CvField[] fields) =>
                new(category, value, 1, page.Text.IndexOf(quote, StringComparison.Ordinal), quote) { Fields = fields };
            return Task.FromResult(new CvProviderResponse(new([
                Fact("employment", "Engineer", "Acme Engineer 2020-01 2022-12", new CvField("company", "Acme", "Acme"), new CvField("position", "Engineer", "Engineer"), new CvField("start", "2020-01", "2020-01"), new CvField("end", "2022-12", "2022-12")),
                Fact("education", "Example University", "Example University Computer Science BSc 2015-2019", new CvField("institution", "Example University", "Example University"), new CvField("degree", "BSc", "BSc")),
                Fact("skill", "C#", "C#"), Fact("language", "English", "English B2", new CvField("level", "B2", "B2"))
            ], []), "synthetic-structured", 100, 50));
        };
        try { await RunBrowserAsync(candidate, "portfolio-review.spec.ts", "portfolio-review"); }
        finally { fixture.Provider.Override = null; }
    }
    private sealed record CreatedCandidate(Guid Id);
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
            var process = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root.FullName, "NexusHR.Web"), RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "node_modules/@playwright/test/cli.js", "test", file, "--grep", filter }) process.ArgumentList.Add(arg);
            foreach (var arg in new[] { "--output", Path.Combine(root.FullName, ".tools", "browser-fixtures", candidate.ToString()), "--reporter=list" }) process.ArgumentList.Add(arg);
            process.Environment["E2E_AI_BACKEND_URL"] = fixture.App.Urls.Single();
            process.Environment["E2E_AI_CANDIDATE_ID"] = candidate.ToString();
            process.Environment["E2E_EMPTY_KIND"] = kind;
            process.Environment["E2E_PORTFOLIO_PDF"] = Convert.ToBase64String(AiFixture.Pdf);
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
