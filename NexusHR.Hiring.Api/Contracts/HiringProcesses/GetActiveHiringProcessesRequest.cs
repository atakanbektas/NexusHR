namespace NexusHR.Hiring.Api
    .Contracts.HiringProcesses;

public sealed record GetActiveHiringProcessesRequest(
    Guid[]? CandidateIds);