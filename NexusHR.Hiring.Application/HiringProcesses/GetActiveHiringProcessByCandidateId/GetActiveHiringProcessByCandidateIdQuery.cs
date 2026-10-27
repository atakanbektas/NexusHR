using MediatR;

namespace NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateId;

public sealed record GetActiveHiringProcessByCandidateIdQuery(
    Guid CandidateId)
    : IRequest<ActiveHiringProcessLookupResponse>;