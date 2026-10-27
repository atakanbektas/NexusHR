using MediatR;
using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.Application.HiringProcesses.CreateHiringProcess;

public sealed record CreateHiringProcessCommand(
    Guid CandidateId,
    string PositionTitle,
    string Department,
    EmploymentType EmploymentType)
    : IRequest<CreateHiringProcessResponse>;