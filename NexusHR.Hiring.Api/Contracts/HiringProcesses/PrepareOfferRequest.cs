namespace NexusHR.Hiring.Api.Contracts.HiringProcesses;

public sealed record PrepareOfferRequest(
    decimal GrossSalary,
    string Currency,
    DateOnly ProposedStartDate,
    DateTime OfferExpiresAtUtc);