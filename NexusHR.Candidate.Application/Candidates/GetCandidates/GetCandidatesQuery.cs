using MediatR;
using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Candidates.GetCandidates;

public sealed record GetCandidatesQuery(
    int Page,
    int PageSize,
    string? Search,
    CandidateStatus? Status)
    : IRequest<GetCandidatesResponse>;
