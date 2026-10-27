using MediatR;

namespace NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateIds;

public sealed record GetActiveHiringProcessesByCandidateIdsQuery(
    IReadOnlyCollection<Guid> CandidateIds)
    : IRequest<
        IReadOnlyCollection<
            CandidateHiringProcessStatusResponse>>;
