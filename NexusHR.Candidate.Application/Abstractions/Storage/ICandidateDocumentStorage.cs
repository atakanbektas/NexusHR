namespace NexusHR.Candidate.Application.Abstractions.Storage;

public interface ICandidateDocumentStorage
{
    Task UploadAsync(
        string objectName,
        byte[] fileContent,
        string contentType,
        CancellationToken cancellationToken);

    Task<byte[]> DownloadAsync(
        string objectName,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken);
}