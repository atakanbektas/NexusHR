namespace NexusHR.Candidate.Application.Candidates.CreateCandidate;

public sealed record CreateCandidateResponse(
    bool IsSuccess,
    Guid? CandidateId,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static CreateCandidateResponse Success(Guid candidateId)
    {
        return new CreateCandidateResponse(
            IsSuccess: true,
            CandidateId: candidateId,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static CreateCandidateResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new CreateCandidateResponse(
            IsSuccess: false,
            CandidateId: null,
            ErrorCode: errorCode,
            Errors: errors);
    }
}