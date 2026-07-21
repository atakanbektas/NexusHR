namespace NexusHR.Candidate.Application.Candidates.GetCandidateById;

public sealed record GetCandidateByIdResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);