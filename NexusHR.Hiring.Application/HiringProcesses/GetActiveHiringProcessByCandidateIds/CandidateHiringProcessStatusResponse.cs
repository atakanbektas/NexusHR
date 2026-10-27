namespace NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateIds;

public sealed record CandidateHiringProcessStatusResponse(
    Guid CandidateId,
    Guid HiringProcessId,
    string Status);
