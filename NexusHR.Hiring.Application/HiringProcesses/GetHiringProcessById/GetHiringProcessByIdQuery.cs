using MediatR;

namespace NexusHR.Hiring.Application.HiringProcesses.GetHiringProcessById;

public sealed record GetHiringProcessByIdQuery(
    Guid HiringProcessId)
    : IRequest<HiringProcessDetailResponse?>;