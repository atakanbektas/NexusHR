using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Infrastructure.Persistence;
using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Infrastructure.Repositories;

internal sealed class CandidateRepository(
    CandidateDbContext dbContext)
    : ICandidateRepository
{
    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken)
    {
        return dbContext.Candidates.AnyAsync(
            candidate => candidate.Email == email,
            cancellationToken);
    }

    public async Task AddAsync(
        CandidateEntity candidate,
        CancellationToken cancellationToken)
    {
        await dbContext.Candidates.AddAsync(
            candidate,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CandidateEntity>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await dbContext.Candidates
            .AsNoTracking()
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        CancellationToken cancellationToken)
    {
        return dbContext.Candidates.CountAsync(cancellationToken);
    }

    public Task<CandidateEntity?> GetByIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        return dbContext.Candidates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == candidateId,
                cancellationToken);
    }
}