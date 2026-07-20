namespace NexusHR.Candidate.Api.Contracts.Candidates;

public sealed record CreateCandidateRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber);