using NexusHR.Candidate.Application.Abstractions.Documents;

namespace NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;

public sealed record ExtractCandidateFromCvResponse(
    bool IsSuccess,
    ExtractedCandidateDraft? Draft,
    string? ErrorCode,
    IReadOnlyCollection<string> Errors)
{
    public static ExtractCandidateFromCvResponse Success(
        ExtractedCandidateDraft draft)
    {
        return new ExtractCandidateFromCvResponse(
            IsSuccess: true,
            Draft: draft,
            ErrorCode: null,
            Errors: Array.Empty<string>());
    }

    public static ExtractCandidateFromCvResponse Failure(
        string errorCode,
        params string[] errors)
    {
        return new ExtractCandidateFromCvResponse(
            IsSuccess: false,
            Draft: null,
            ErrorCode: errorCode,
            Errors: errors);
    }
}