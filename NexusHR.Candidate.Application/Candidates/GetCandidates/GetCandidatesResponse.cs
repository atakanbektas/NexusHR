namespace NexusHR.Candidate.Application.Candidates.GetCandidates;

public sealed record CandidateListItemResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Status,
    DateTime CreatedAtUtc);

public sealed record GetCandidatesResponse(
    IReadOnlyCollection<CandidateListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);