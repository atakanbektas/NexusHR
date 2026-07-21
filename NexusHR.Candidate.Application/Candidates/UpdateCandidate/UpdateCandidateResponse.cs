namespace NexusHR.Candidate.Application.Candidates.UpdateCandidate;

public sealed record UpdateCandidateResponse(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static UpdateCandidateResponse Success()
    {
        return new UpdateCandidateResponse(
            IsSuccess: true,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static UpdateCandidateResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new UpdateCandidateResponse(
            IsSuccess: false,
            ErrorCode: errorCode,
            Errors: errors);
    }
}