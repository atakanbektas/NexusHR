namespace NexusHR.Contracts.Onboarding;

public sealed record EmployeeOnboardingCompletedIntegrationEvent(
    Guid EventId, Guid HiringProcessId, Guid CandidateId, Guid EmployeeId,
    DateTime OccurredAtUtc, int SchemaVersion = 1);
