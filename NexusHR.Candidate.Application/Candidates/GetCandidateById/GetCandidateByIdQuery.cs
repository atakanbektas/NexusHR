using MediatR;

namespace NexusHR.Candidate.Application.Candidates.GetCandidateById;

public sealed record GetCandidateByIdQuery(Guid CandidateId)
    : IRequest<GetCandidateByIdResponse?>;