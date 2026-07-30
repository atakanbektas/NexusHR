using Microsoft.EntityFrameworkCore;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Domain.HiringProcesses;
using NexusHR.Hiring.Infrastructure.Persistence;

using HiringProcessEntity =
    NexusHR.Hiring.Domain.HiringProcesses.HiringProcess;

namespace NexusHR.Hiring.Infrastructure.Repositories;

internal sealed class HiringProcessRepository(
    HiringDbContext dbContext)
    : IHiringProcessRepository
{
    private static readonly HiringProcessStatus[] ActiveStatuses =
    [
        HiringProcessStatus.Draft,
        HiringProcessStatus.OfferPrepared,
        HiringProcessStatus.OfferSent,
        HiringProcessStatus.OfferAccepted
    ];

    public async Task<HiringProcessEntity?> GetByIdAsync(
        Guid hiringProcessId,
        CancellationToken cancellationToken)
    {
        return await dbContext.HiringProcesses
            .SingleOrDefaultAsync(
                hiringProcess =>
                    hiringProcess.Id == hiringProcessId,
                cancellationToken);
    }

    public async Task<HiringProcessEntity?>
    GetByOfferResponseTokenHashAsync(
        string offerResponseTokenHash,
        CancellationToken cancellationToken)
    {
        return await dbContext.HiringProcesses
            .SingleOrDefaultAsync(
                hiringProcess =>
                    hiringProcess.OfferResponseTokenHash ==
                        offerResponseTokenHash &&
                    hiringProcess.Status ==
                        HiringProcessStatus.OfferSent,
                cancellationToken);
    }

    public async Task<HiringProcessEntity?>
        GetActiveByCandidateIdAsync(
            Guid candidateId,
            CancellationToken cancellationToken)
    {
        return await dbContext.HiringProcesses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                hiringProcess =>
                    hiringProcess.CandidateId == candidateId &&
                    ActiveStatuses.Contains(
                        hiringProcess.Status),
                cancellationToken);
    }

    public async Task<bool> HasActiveProcessAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        return await dbContext.HiringProcesses
            .AsNoTracking()
            .AnyAsync(
                hiringProcess =>
                    hiringProcess.CandidateId == candidateId &&
                    ActiveStatuses.Contains(
                        hiringProcess.Status),
                cancellationToken);
    }

    public async Task AddAsync(
        HiringProcessEntity hiringProcess,
        CancellationToken cancellationToken)
    {
        await dbContext.HiringProcesses.AddAsync(
            hiringProcess,
            cancellationToken);
    }

    public Task UpdateAsync(
        HiringProcessEntity hiringProcess,
        CancellationToken cancellationToken)
    {
        dbContext.HiringProcesses.Update(
            hiringProcess);

        return Task.CompletedTask;
    }

    public async Task<
    IReadOnlyCollection<HiringProcessEntity>>
    GetActiveByCandidateIdsAsync(
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.HiringProcesses
            .AsNoTracking()
            .Where(hiringProcess =>
                candidateIds.Contains(
                    hiringProcess.CandidateId) &&
                ActiveStatuses.Contains(
                    hiringProcess.Status))
            .ToListAsync(cancellationToken);
    }
}