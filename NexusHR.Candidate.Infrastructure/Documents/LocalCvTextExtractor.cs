using System.Net;
using System.Net.Http.Json;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Infrastructure.Documents;

internal sealed class LocalCvTextExtractor(HttpClient http, LocalDocumentOptions options, ICvDocumentScanner scanner) : ICvTextExtractor
{
    public async Task<IReadOnlyList<CvTextPage>> ExtractPagesAsync(byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length is 0 or > 5 * 1024 * 1024 || CvFileSignature.ContentType(bytes) is null) throw new InvalidDataException("Invalid CV file.");
        await scanner.ScanAsync(bytes, ct);
        var pdf = bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        IReadOnlyList<CvTextPage>? original = null;
        if (pdf)
        {
            try { original = await new PdfCvTextExtractor().ExtractPagesAsync(bytes, ct); }
            catch (CvTextUnavailableException) when (options.Enabled) { }
            if (original is not null && (!options.Enabled || original.All(p => !p.RequiresOcr)))
                return original;
        }
        if (!options.Enabled) throw new CvProviderException("AI.OcrDisabled");
        if (bytes.Length is 0 or > 5 * 1024 * 1024) throw new InvalidDataException("Invalid document size.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            using var body = new ByteArrayContent(bytes);
            using var request = new HttpRequestMessage(HttpMethod.Post, "extract") { Content = body };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new CvProviderException("AI.OcrBusy", true, TimeSpan.FromSeconds(5));
            if (!response.IsSuccessStatusCode) throw new CvProviderException("AI.OcrInvalidDocument");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(chunk, deadline.Token)) > 0)
            {
                if (buffer.Length + count > 12 * 1024 * 1024) throw new InvalidDataException("OCR response exceeds limit.");
                buffer.Write(chunk, 0, count);
            }
            buffer.Position = 0;
            var result = await System.Text.Json.JsonSerializer.DeserializeAsync<OcrResult>(buffer, CvAnalyses.CvAnalysisStore.Json, deadline.Token)
                ?? throw new InvalidDataException("Invalid OCR response.");
            if (result.Pages is null || result.Pages.Count is < 1 or > 8 || result.Pages.Sum(p => (long)p.Text.Length) > 100000)
                throw new InvalidDataException("Invalid OCR page budget.");
            if (original is not null && result.Pages.Count != original.Count) throw new InvalidDataException("OCR page count changed.");
            var pages = new List<CvTextPage>();
            for (var i = 0; i < result.Pages.Count; i++)
            {
                var p = result.Pages[i];
                if (p.Number != i + 1 || p.Width is < 1 or > 1800 || p.Height is < 1 or > 1800 || p.Words.Count > 10000 ||
                    p.Words.Any(w => w.Start < 0 || w.Length <= 0 || (long)w.Start + w.Length > p.Text.Length || w.X < 0 || w.Y < 0 ||
                        w.Width <= 0 || w.Height <= 0 || (long)w.X + w.Width > p.Width || (long)w.Y + w.Height > p.Height ||
                        !double.IsFinite(w.Confidence) || w.Confidence is < 0 or > 100)) throw new InvalidDataException("Invalid OCR geometry.");
                var prior = original?.Single(x => x.Number == p.Number);
                // Mixed pages use one authoritative text/coordinate system, never concatenate mismatched offsets.
                pages.Add(prior is not null && !prior.RequiresOcr ? prior :
                    new(p.Number, p.Text) { Source = "tesseract-5-tur-eng", ImageCount = 1, Words = p.Words });
            }
            if (pages.All(p => !CvAnalyses.CvAnalysisInput.HasReadableText(p)))
                throw new CvProviderException("AI.OcrNoReadableText");
            return pages;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new CvProviderException("AI.OcrTimeout"); }
        catch (HttpRequestException) { throw new CvProviderException("AI.OcrUnavailable"); }
        catch (System.Text.Json.JsonException) { throw new InvalidDataException("Invalid OCR response."); }
    }
    private sealed record OcrResult(IReadOnlyList<OcrPage> Pages);
    private sealed record OcrPage(int Number, string Text, int Width, int Height, IReadOnlyList<CvWordBox> Words);
}
