using MediatR;
using NexusHR.Hiring.Application.Abstractions.Persistence;

namespace NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateIds;

internal sealed class GetActiveHiringProcessesByCandidateIdsQueryHandler(
    IHiringProcessRepository hiringProcessRepository)
    : IRequestHandler<
        GetActiveHiringProcessesByCandidateIdsQuery,
        IReadOnlyCollection<
            CandidateHiringProcessStatusResponse>>
{
    public async Task<
        IReadOnlyCollection<
            CandidateHiringProcessStatusResponse>>
        Handle(
            GetActiveHiringProcessesByCandidateIdsQuery request,
            CancellationToken cancellationToken)
    {
        var candidateIds =
            request.CandidateIds
                .Where(candidateId =>
                    candidateId != Guid.Empty)
                .Distinct()
                .ToArray();

        if (candidateIds.Length == 0)
        {
            return Array.Empty<
                CandidateHiringProcessStatusResponse>();
        }

        var hiringProcesses =
            await hiringProcessRepository
                .GetLatestByCandidateIdsAsync(
                    candidateIds,
                    cancellationToken);

        return hiringProcesses
            .Select(hiringProcess =>
                new CandidateHiringProcessStatusResponse(
                    hiringProcess.CandidateId,
                    hiringProcess.Id,
                    hiringProcess.Status.ToString()))
            .ToArray();
    }
}
