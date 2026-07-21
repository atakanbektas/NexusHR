using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Infrastructure.Persistence;

namespace NexusHR.Candidate.Infrastructure.Repositories;

internal sealed class CandidateDocumentRepository(
    CandidateDbContext dbContext)
    : ICandidateDocumentRepository
{
    public Task<bool> ExistsAsync(
        Guid candidateId,
        CandidateDocumentType type,
        CancellationToken cancellationToken)
    {
        return dbContext.CandidateDocuments.AnyAsync(
            document =>
                document.CandidateId == candidateId &&
                document.Type == type,
            cancellationToken);
    }

    public Task<CandidateDocument?> GetAsync(
        Guid candidateId,
        CandidateDocumentType type,
        CancellationToken cancellationToken)
    {
        return dbContext.CandidateDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document =>
                    document.CandidateId == candidateId &&
                    document.Type == type,
                cancellationToken);
    }

    public async Task AddAsync(
        CandidateDocument document,
        CancellationToken cancellationToken)
    {
        await dbContext.CandidateDocuments.AddAsync(
            document,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}