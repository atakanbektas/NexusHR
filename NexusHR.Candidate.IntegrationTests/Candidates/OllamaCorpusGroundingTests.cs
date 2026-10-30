using System.Security.Cryptography;
using System.Text.Json;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class OllamaCorpusGroundingTests
{
    [CorpusGroundingFact]
    public async Task EveryPersistedCorpusFactHasExactEvidenceInTheActualStoredPdf()
    {
        // Run in Linux, matching the deployed PdfPig newline/UTF-16 source representation.
        Assert.True(OperatingSystem.IsLinux(), "Use the documented Linux SDK container for deployed-source verification.");
        var directory = Environment.GetEnvironmentVariable("OLLAMA_CORPUS_DIR") ?? throw new InvalidOperationException("Synthetic corpus directory required.");
        var cases = new[] { "tr-backend", "tr-frontend", "tr-data", "tr-devops", "tr-qa", "en-fullstack", "en-backend", "en-cloud", "en-data", "en-quality" };
        foreach (var name in cases)
        {
            var saved = JsonSerializer.Deserialize<SavedCase>(await File.ReadAllTextAsync(Path.Combine(directory, name + ".json")), CvAnalysisStore.Json)!;
            Assert.Equal(name, saved.Case); Assert.StartsWith("synthetic-corpus-" + name + "-", saved.Email); Assert.EndsWith("@example.test", saved.Email);
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, name + ".pdf"));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), saved.Run.ContentHash);
            var pages = await new PdfCvTextExtractor().ExtractPagesAsync(bytes, default);
            var input = CvAnalysisInput.Prepare(pages, "Synthetic", "Expert", saved.Email, "05555555555");
            Assert.Equal("Succeeded", saved.Run.Status); Assert.NotNull(saved.Run.Result); Assert.NotEmpty(saved.Run.Result.Facts);
            EvidenceValidator.Validate(saved.Run.Result, input.Pages);
            foreach (var fact in saved.Run.Result.Facts)
            {
                var source = input.Pages.Single(p => p.Number == fact.Page).Text;
                Assert.Equal(fact.Quote, source.Substring(fact.Start, fact.Quote.Length));
                Assert.Contains(fact.Value, fact.Quote, StringComparison.Ordinal);
            }
            foreach (var category in new[] { "skill", "employment", "education" })
                Assert.Contains(saved.Run.Result.Facts, f => f.Category == category);
        }
    }
    private sealed record SavedCase(string Case, string Email, AnalysisView Run);
}
internal sealed class CorpusGroundingFactAttribute : FactAttribute
{
    public CorpusGroundingFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_OLLAMA_CORPUS_VERIFY") != "1")
            Skip = "Opt-in verification of actual stored synthetic PDFs/results after the real-model browser corpus.";
    }
}
