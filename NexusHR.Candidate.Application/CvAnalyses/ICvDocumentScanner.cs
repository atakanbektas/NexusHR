namespace NexusHR.Candidate.Application.CvAnalyses;

public interface ICvDocumentScanner
{
    Task ScanAsync(byte[] content, CancellationToken ct);
}
