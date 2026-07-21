namespace NexusHR.Candidate.Api.Contracts.Candidates;

public sealed record UpdateCandidateRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber);