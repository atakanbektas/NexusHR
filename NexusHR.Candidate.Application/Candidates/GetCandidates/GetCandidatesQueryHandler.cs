using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;

namespace NexusHR.Candidate.Application.Candidates.GetCandidates;

internal sealed class GetCandidatesQueryHandler(
    ICandidateRepository candidateRepository)
    : IRequestHandler<GetCandidatesQuery, GetCandidatesResponse>
{
    public async Task<GetCandidatesResponse> Handle(
        GetCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1
            ? 1
            : request.Page;

        var pageSize = request.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => request.PageSize
        };

        // An extreme client-supplied page must never wrap into a negative/earlier offset.
        var offset = ((long)page - 1) * pageSize;
        var candidates = offset > int.MaxValue ? [] : await candidateRepository.GetPageAsync(
            skip: (int)offset,
            take: pageSize,
            search: request.Search,
            status: request.Status,
            cancellationToken);

        var totalCount = await candidateRepository.CountAsync(
            request.Search,
            request.Status,
            cancellationToken);

        var items = candidates
            .Select(candidate => new CandidateListItemResponse(
                candidate.Id,
                candidate.FirstName,
                candidate.LastName,
                candidate.Email,
                candidate.PhoneNumber,
                candidate.Status.ToString(),
                candidate.CreatedAtUtc))
            .ToArray();

        return new GetCandidatesResponse(
            items,
            page,
            pageSize,
            totalCount);
    }
}
