using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.DataModel.Args;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.Api;

internal sealed class DocumentReadiness(IMinioClient storage, IConfiguration configuration,
    ICvDocumentScanner scanner, LocalDocumentOptions documents) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            // false is a valid response on a fresh install; upload lazily initializes the bucket.
            _ = await storage.BucketExistsAsync(new BucketExistsArgs().WithBucket(configuration["Minio:BucketName"]!), timeout.Token);
            await scanner.ScanAsync("NexusHR readiness"u8.ToArray(), timeout.Token);
            if (documents.Enabled)
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
                using var response = await http.GetAsync(new Uri(new Uri(documents.BaseUrl), "health"), timeout.Token);
                if (!response.IsSuccessStatusCode) return HealthCheckResult.Unhealthy("Local OCR unavailable.");
            }
            return HealthCheckResult.Healthy();
        }
        catch { return HealthCheckResult.Unhealthy("Document storage/scanner/OCR dependency unavailable."); }
    }
}
