namespace NexusHR.Candidate.Infrastructure.Storage;

// Durable compensation record. Removed in the same transaction as CV metadata.
public sealed class CvUploadIntent
{
    public Guid Id { get; set; }
    public string ObjectName { get; set; } = "";
    public DateTime ReconcileAfterUtc { get; set; }
}
