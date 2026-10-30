using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class CvAnalysisInputTests
{
    [Fact]
    public void ProfessionalIsoDatesSurviveWithoutExposingContactFieldsOrChangingOffsets()
    {
        const string source = "Engineer 2020-01 2022-12\nEducation 2015-09-01 2019-06-30\nPhone +90 (555) 123 45 67\ncontact@example.test";
        var page = Assert.Single(CvAnalysisInput.Prepare([new(1, source)]).Pages);
        Assert.Contains("2020-01 2022-12", page.Text); Assert.Contains("2015-09-01 2019-06-30", page.Text);
        Assert.DoesNotContain("555", page.Text); Assert.DoesNotContain("contact@example.test", page.Text); Assert.Equal(source.Length, page.Text.Length);
    }
    [Theory]
    [InlineData("Engineer phone 2020-01 2022-12")]
    [InlineData("Contact 2020-01-01")]
    [InlineData("Engineer +44 7700 900123")]
    [InlineData("Engineer 2020-99 2022-99")]
    public void ContactContextAndInvalidDatesRemainMasked(string source)
    {
        var input = CvAnalysisInput.Prepare([new(1, source)]);
        Assert.DoesNotContain(Assert.Single(input.Pages).Text, char.IsDigit);
    }
    [Fact]
    public void KnownDateShapedPhoneIdentifierIsAlwaysRedacted()
    {
        var page = Assert.Single(CvAnalysisInput.Prepare([new(1, "Engineer 2020-01-01")], "2020-01-01").Pages);
        Assert.DoesNotContain(page.Text, char.IsDigit);
    }
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
