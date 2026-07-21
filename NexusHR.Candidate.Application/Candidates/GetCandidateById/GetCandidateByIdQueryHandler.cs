using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;

namespace NexusHR.Candidate.Application.Candidates.GetCandidateById;

internal sealed class GetCandidateByIdQueryHandler(
    ICandidateRepository candidateRepository)
    : IRequestHandler<GetCandidateByIdQuery, GetCandidateByIdResponse?>
{
    public async Task<GetCandidateByIdResponse?> Handle(
        GetCandidateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var candidate = await candidateRepository.GetByIdAsync(
            request.CandidateId,
            cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        return new GetCandidateByIdResponse(
            candidate.Id,
            candidate.FirstName,
            candidate.LastName,
            candidate.Email,
            candidate.PhoneNumber,
            candidate.Status.ToString(),
            candidate.CreatedAtUtc,
            candidate.UpdatedAtUtc);
    }
}