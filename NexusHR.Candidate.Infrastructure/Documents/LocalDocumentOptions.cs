namespace NexusHR.Candidate.Infrastructure.Documents;

public sealed class LocalDocumentOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://cv-ocr:8080";
    public void Validate()
    {
        if (Enabled && (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != "http" || (uri.Host != "cv-ocr" && !uri.IsLoopback) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0))
            throw new InvalidOperationException("OCR requires an internal Docker or loopback endpoint.");
    }
}
