namespace NexusHR.Contracts.Hiring;

public sealed record HiringOfferSentIntegrationEvent(
    Guid EventId,
    Guid HiringProcessId,
    Guid CandidateId,
    string CandidateFirstName,
    string CandidateLastName,
    string CandidateEmail,
    string PositionTitle,
    string Department,
    decimal GrossSalary,
    string Currency,
    DateOnly ProposedStartDate,
    DateTime OfferExpiresAtUtc,
    string ResponseToken,
    DateTime OccurredAtUtc,
    int SchemaVersion = 1);