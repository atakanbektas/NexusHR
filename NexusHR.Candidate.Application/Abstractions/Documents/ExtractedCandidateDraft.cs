namespace NexusHR.Candidate.Application.Abstractions.Documents;

public sealed record ExtractedCandidateDraft(
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber);