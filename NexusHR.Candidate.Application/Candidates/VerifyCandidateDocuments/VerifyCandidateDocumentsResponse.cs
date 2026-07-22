namespace NexusHR.Candidate.Application.Candidates.VerifyCandidateDocuments;

public sealed record VerifyCandidateDocumentsResponse(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static VerifyCandidateDocumentsResponse Success()
    {
        return new VerifyCandidateDocumentsResponse(
            IsSuccess: true,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static VerifyCandidateDocumentsResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new VerifyCandidateDocumentsResponse(
            IsSuccess: false,
            ErrorCode: errorCode,
            Errors: errors);
    }
}