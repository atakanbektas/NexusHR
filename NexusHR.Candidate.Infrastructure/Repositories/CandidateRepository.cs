using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Domain.Candidates;
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
        string? search,
        CandidateStatus? status,
        CancellationToken cancellationToken)
    {
        return await ApplyFilters(search, status)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        string? search,
        CandidateStatus? status,
        CancellationToken cancellationToken)
    {
        return ApplyFilters(search, status)
            .CountAsync(cancellationToken);
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

    public Task<bool> EmailExistsAsync(
        string email,
        Guid excludedCandidateId,
        CancellationToken cancellationToken)
    {
        return dbContext.Candidates.AnyAsync(
            candidate =>
                candidate.Email == email &&
                candidate.Id != excludedCandidateId,
            cancellationToken);
    }

    public async Task UpdateAsync(
        CandidateEntity candidate,
        CancellationToken cancellationToken)
    {
        dbContext.Candidates.Update(candidate);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private IQueryable<CandidateEntity> ApplyFilters(
        string? search,
        CandidateStatus? status)
    {
        var query = dbContext.Candidates.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(
                candidate => candidate.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(candidate =>
                EF.Functions.ILike(candidate.FirstName, pattern) ||
                EF.Functions.ILike(candidate.LastName, pattern) ||
                EF.Functions.ILike(candidate.Email, pattern) ||
                EF.Functions.ILike(candidate.PhoneNumber, pattern));
        }

        return query;
    }
}
