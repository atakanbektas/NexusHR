namespace NexusHR.Contracts.Hiring;

public sealed record HiringOfferAcceptedIntegrationEvent(
    Guid EventId,
    Guid HiringProcessId,
    Guid CandidateId,
    string CandidateFirstName,
    string CandidateLastName,
    string CandidateEmail,
    string PositionTitle,
    string Department,
    DateOnly ProposedStartDate,
    DateTime OccurredAtUtc,
    int SchemaVersion = 1);
