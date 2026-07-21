using MediatR;

namespace NexusHR.Candidate.Application.Candidates.GetCandidates;

public sealed record GetCandidatesQuery(
    int Page,
    int PageSize)
    : IRequest<GetCandidatesResponse>;