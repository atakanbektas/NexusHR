using Microsoft.EntityFrameworkCore;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Domain.EligibleCandidates;
using NexusHR.Hiring.Infrastructure.Persistence;

namespace NexusHR.Hiring.Infrastructure.Repositories;

internal sealed class EligibleCandidateRepository(
    HiringDbContext dbContext)
    : IEligibleCandidateRepository
{
    public async Task<EligibleCandidate?> GetByCandidateIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EligibleCandidates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CandidateId == candidateId,
                cancellationToken);
    }

    public async Task AddAsync(
        EligibleCandidate candidate,
        CancellationToken cancellationToken)
    {
        await dbContext.EligibleCandidates.AddAsync(
            candidate,
            cancellationToken);
    }
}