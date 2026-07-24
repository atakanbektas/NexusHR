using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.Api.Contracts.HiringProcesses;

public sealed record CreateHiringProcessRequest(
    Guid CandidateId,
    string PositionTitle,
    string Department,
    EmploymentType EmploymentType);