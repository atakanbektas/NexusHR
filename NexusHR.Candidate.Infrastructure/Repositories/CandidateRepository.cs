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
}