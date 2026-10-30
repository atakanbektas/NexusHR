using System.Net;
using System.Text.Json;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Application.Candidates.UploadCandidateCv;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class MultimodalContractTests
{
    [Fact]
    public void StructuredFieldsCannotIntroduceUngroundedValues()
    {
        var page = new CvTextPage(1, "Acme Engineer 2020-01 2022-12");
        var fact = new CvFact("employment", "Engineer", 1, 0, page.Text) {
            Fields = [new("company", "Acme", "Acme"), new("start", "2020-01", "2020-01"), new("end", "2022-12", "2022-12")] };
        EvidenceValidator.Validate(new([fact], []), [page]);
        Assert.Throws<InvalidDataException>(() => EvidenceValidator.Validate(new([fact with { Fields = [new("company", "Invented", "Acme")] }], []), [page]));
        Assert.Equal(36, ExperienceCoverage.CalendarMonths([fact]));
    }
    [Fact]
    public void ExperienceCoverageMergesOverlapsAndSkipsAmbiguousOrMissingDates()
    {
        static CvFact Work(string start, string end) => new("employment", "Engineer", 1, 0, "Engineer") {
            Fields = [new("start", start, start), new("end", end, end)] };
        Assert.Equal(36, ExperienceCoverage.CalendarMonths([Work("2020-01", "2021-12"), Work("2021-06", "2022-12"), Work("01/02/2020", "Present")]));
        Assert.Null(ExperienceCoverage.CalendarMonths([Work("Present", "2020"), Work("2022-01", "2020-01")]));
    }
    [Theory]
    [InlineData("cv.svg", "image/svg+xml")]
    [InlineData("cv.jpg", "application/pdf")]
    [InlineData("cv.pdf", "image/jpeg")]
    public void UploadRejectsExtensionMimeSignatureMismatch(string name, string mime)
    {
        Assert.False(new UploadCandidateCvCommandValidator().Validate(new UploadCandidateCvCommand(Guid.NewGuid(), name, mime, AiFixture.Pdf)).IsValid);
    }
    [Fact]
    public async Task TextPdfDoesNotCallOcrOrChangeExistingOffsets()
    {
        var handler = new Handler(_ => throw new InvalidOperationException("OCR must not be called"));
        var extractor = new LocalCvTextExtractor(new HttpClient(handler) { BaseAddress = new Uri("http://cv-ocr:8080") }, new(), new ClamAvDocumentScanner(new()));
        var expected = await new PdfCvTextExtractor().ExtractPagesAsync(AiFixture.Pdf, default);
        var actual = await extractor.ExtractPagesAsync(AiFixture.Pdf, default);
        Assert.Equal(expected[0].Text, actual[0].Text); Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task OcrGeometryAndOffsetsAreAuthoritativeAndNotSentToExternalProvider()
    {
        var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("""
          {"pages":[{"number":1,"text":"C# Engineer","width":100,"height":50,"source":"ignored",
          "words":[{"start":0,"length":2,"x":1,"y":1,"width":20,"height":10,"confidence":98}],"image":"AA=="}]}
          """) });
        var extractor = new LocalCvTextExtractor(new HttpClient(handler) { BaseAddress = new Uri("http://cv-ocr:8080") }, new() { Enabled = true }, new ClamAvDocumentScanner(new()));
        var page = Assert.Single(await extractor.ExtractPagesAsync([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0], default));
        Assert.Single(page.Words); Assert.StartsWith("tesseract", page.Source);
        var json = JsonSerializer.Serialize(page, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("image", json); Assert.DoesNotContain("words", json); Assert.DoesNotContain("source", json);
    }
    internal sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(reply(request)); }
    }
}
