using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class CvAnalysisInputTests
{
    [Fact]
    public async Task ReproducesContactOnlyPdfBecomingEmptyAfterMinimization()
    {
        var raw = await new PdfCvTextExtractor().ExtractPagesAsync(AiFixture.ContactOnlyPdf, default);
        Assert.Contains(raw[0].Text, char.IsLetter);
        var prepared = CvAnalysisInput.Prepare(raw, "Ada", "Lovelace", "synthetic-demo-000@example.test", "05555555555");
        var metrics = Assert.Single(prepared.Metrics);
        Assert.True(metrics.RawReadableCharacters > 0); Assert.Equal(0, metrics.MinimizedReadableCharacters);
        Assert.Equal(metrics.RawCharacters, metrics.MinimizedCharacters); Assert.Empty(prepared.Pages);
    }
    [Theory]
    [InlineData("Teknik beceriler: C#, .NET, PostgreSQL. Yazılım geliştirici.")]
    [InlineData("Technical skills: C#, .NET, PostgreSQL. Software developer.")]
    public void MinimizationPreservesProfessionalTextAndExactOffsets(string professional)
    {
        var text = "Ada Lovelace\nsyn@example.test\n05555555555\n" + professional;
        var prepared = CvAnalysisInput.Prepare([new(1, text)], "Ada", "Lovelace", "syn@example.test", "05555555555");
        var page = Assert.Single(prepared.Pages);
        Assert.DoesNotContain("Ada", page.Text); Assert.DoesNotContain("syn@example.test", page.Text);
        Assert.Contains(professional, page.Text); Assert.Equal(text.Length, page.Text.Length);
        Assert.Equal(text.IndexOf("C#", StringComparison.Ordinal), page.Text.IndexOf("C#", StringComparison.Ordinal));
    }
    [Fact]
    public void BlankCoverPageIsNotSentButOriginalPageNumbersAndImageCoverageRemain()
    {
        var prepared = CvAnalysisInput.Prepare([new(1, "Ada Lovelace") { ImageCount = 1 }, new(2, "C# PostgreSQL")], "Ada", "Lovelace");
        Assert.Equal(2, Assert.Single(prepared.Pages).Number); Assert.Equal(1, prepared.ExcludedPages); Assert.Equal(1, prepared.ImagePages);
    }
    [Fact]
    public async Task ImageOnlyPdfHasExplicitOcrErrorRatherThanEmptySuccess()
    {
        var error = await Assert.ThrowsAsync<CvTextUnavailableException>(() => new PdfCvTextExtractor().ExtractPagesAsync(AiFixture.ImageOnlyPdf, default));
        Assert.Equal(1, error.PageCount); Assert.Equal(1, error.ImagePageCount); Assert.Contains("OCR", error.Message);
    }
}
