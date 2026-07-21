namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

public sealed record UploadCandidateCvResponse(
    bool IsSuccess,
    Guid? DocumentId,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static UploadCandidateCvResponse Success(
        Guid documentId)
    {
        return new UploadCandidateCvResponse(
            IsSuccess: true,
            DocumentId: documentId,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static UploadCandidateCvResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new UploadCandidateCvResponse(
            IsSuccess: false,
            DocumentId: null,
            ErrorCode: errorCode,
            Errors: errors);
    }
}