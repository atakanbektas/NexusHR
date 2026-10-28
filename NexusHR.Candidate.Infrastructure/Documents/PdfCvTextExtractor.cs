using System.Text;
using NexusHR.Candidate.Application.CvAnalyses;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace NexusHR.Candidate.Infrastructure.Documents;

internal sealed class PdfCvTextExtractor : ICvTextExtractor
{
    public Task<IReadOnlyList<CvTextPage>> ExtractPagesAsync(byte[] pdf, CancellationToken ct)
    {
        if (pdf.Length > 5 * 1024 * 1024 || pdf.Length < 5 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidDataException("Invalid PDF size/signature.");
        using var document = PdfDocument.Open(pdf);
        if (document.NumberOfPages > 50) throw new InvalidDataException("CV exceeds 50 pages.");
        var pages = new List<CvTextPage>(); var size = 0;
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var text = ContentOrderTextExtractor.GetText(page); size += text.Length + Environment.NewLine.Length;
            if (size > 500_000) throw new InvalidDataException("CV text exceeds the extraction limit.");
            pages.Add(new(page.Number, text) { ImageCount = page.NumberOfImages });
        }
        if (pages.All(x => !x.Text.Any(char.IsLetterOrDigit)))
            throw new CvTextUnavailableException(pages.Count, pages.Count(x => x.ImageCount > 0), pages.Sum(x => x.Text.Length));
        return Task.FromResult<IReadOnlyList<CvTextPage>>(pages);
    }
}
