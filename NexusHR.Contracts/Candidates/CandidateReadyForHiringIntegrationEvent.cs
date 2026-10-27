using System;

namespace NexusHR.Contracts.Candidates;

public sealed record CandidateReadyForHiringIntegrationEvent(
    Guid EventId,
    Guid CandidateId,
    string FirstName,
    string LastName,
    string Email,
    DateTime OccurredAtUtc,
    int SchemaVersion = 1);