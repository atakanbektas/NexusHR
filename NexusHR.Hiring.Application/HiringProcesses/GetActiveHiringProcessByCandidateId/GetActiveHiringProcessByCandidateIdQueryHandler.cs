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
        var hiringProcess =
            await hiringProcessRepository
                .GetActiveByCandidateIdAsync(
                    request.CandidateId,
                    cancellationToken);

        if (hiringProcess is null)
        {
            return ActiveHiringProcessLookupResponse.NotFound();
        }

        return ActiveHiringProcessLookupResponse.Found(
            hiringProcess.Id,
            hiringProcess.Status.ToString());
    }
}