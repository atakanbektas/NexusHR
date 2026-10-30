namespace NexusHR.Candidate.Domain.Candidates;

public sealed class CandidateDocument
{
    private CandidateDocument()
    {
    }

    public CandidateDocument(
        Guid id,
        Guid candidateId,
        CandidateDocumentType type,
        string originalFileName,
        string objectName,
        string contentType,
        long sizeBytes)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Belge kimliği boş olamaz.",
                nameof(id));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException(
                "Aday kimliği boş olamaz.",
                nameof(candidateId));
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException(
                "Dosya adı boş olamaz.",
                nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException(
                "Dosya nesne adı boş olamaz.",
                nameof(objectName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "Dosya içerik türü boş olamaz.",
                nameof(contentType));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes),
                "Dosya boyutu sıfırdan büyük olmalıdır.");
        }

        Id = id;
        CandidateId = candidateId;
        Type = type;
        OriginalFileName = originalFileName.Trim();
        ObjectName = objectName.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        UploadedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CandidateId { get; private set; }

    public CandidateDocumentType Type { get; private set; }

    public string OriginalFileName { get; private set; } = null!;

    public string ObjectName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }
    public string? ContentHash { get; private set; }
    public bool IsCurrent { get; private set; } = true;
    public int Revision { get; private set; } = 1;
    public Guid? PreviousDocumentId { get; private set; }
    public void Replace(CandidateDocument previous)
    {
        if (!previous.IsCurrent || previous.CandidateId != CandidateId || previous.Type != Type)
            throw new InvalidOperationException("Invalid previous document.");
        previous.IsCurrent = false;
        PreviousDocumentId = previous.Id;
        Revision = checked(previous.Revision + 1);
    }
    public void RecordContentHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Invalid SHA-256.");
        if (ContentHash is not null && ContentHash != hash) throw new InvalidOperationException("Immutable document content changed.");
        ContentHash = hash;
    }
}
