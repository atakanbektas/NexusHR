using MediatR;
using NexusHR.Hiring.Application.Abstractions.Persistence;

namespace NexusHR.Hiring.Application.HiringProcesses
    .GetActiveHiringProcessByCandidateId;

internal sealed class
    GetActiveHiringProcessByCandidateIdQueryHandler(
        IHiringProcessRepository hiringProcessRepository)
    : IRequestHandler<
        GetActiveHiringProcessByCandidateIdQuery,
        ActiveHiringProcessLookupResponse>
{
    public async Task<ActiveHiringProcessLookupResponse> Handle(
        GetActiveHiringProcessByCandidateIdQuery request,
        CancellationToken cancellationToken)
    {
        var activeHiringProcess =
            await hiringProcessRepository
                .GetActiveByCandidateIdAsync(
                    request.CandidateId,
                    cancellationToken);

        if (activeHiringProcess is not null)
        {
            return ActiveHiringProcessLookupResponse.Found(
                activeHiringProcess.Id,
                activeHiringProcess.Status.ToString());
        }

        var latestHiringProcess =
            await hiringProcessRepository
                .GetLatestByCandidateIdAsync(
                    request.CandidateId,
                    cancellationToken);

        if (latestHiringProcess is null)
        {
            return ActiveHiringProcessLookupResponse.NotFound();
        }

        return ActiveHiringProcessLookupResponse.Inactive(
            latestHiringProcess.Status.ToString());
    }
}
