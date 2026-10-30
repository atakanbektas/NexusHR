using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Infrastructure.Documents;

internal sealed class LocalDocumentValidator(HttpClient http, LocalDocumentOptions options, ICvDocumentScanner scanner)
{
    public async Task ValidateAsync(byte[] bytes, CancellationToken ct)
    {
        await scanner.ScanAsync(bytes, ct);
        var mime = CvFileSignature.ContentType(bytes) ?? throw new CvAnalysisException(400, "Candidate.CvValidationFailed");
        if (mime == "application/pdf")
        {
            try
            {
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
                if (pdf.NumberOfPages is < 1 or > 50) throw new InvalidDataException();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { throw new CvAnalysisException(400, "Candidate.CvValidationFailed"); }
        }
        if (!options.Enabled) throw new CvAnalysisException(503, "AI.OcrDisabled");
        try
        {
            using var content = new ByteArrayContent(bytes);
            using var request = new HttpRequestMessage(HttpMethod.Post, "validate") { Content = content };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new CvAnalysisException(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? 429 : 400, "Candidate.CvValidationFailed");
        }
        catch (HttpRequestException) { throw new CvAnalysisException(503, "AI.OcrUnavailable"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new CvAnalysisException(503, "AI.OcrTimeout"); }
    }
}
