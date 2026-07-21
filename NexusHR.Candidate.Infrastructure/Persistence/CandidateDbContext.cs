using Microsoft.EntityFrameworkCore;
using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;
using CandidateDocumentEntity =
    NexusHR.Candidate.Domain.Candidates.CandidateDocument;

namespace NexusHR.Candidate.Infrastructure.Persistence;

public sealed class CandidateDbContext(
    DbContextOptions<CandidateDbContext> options)
    : DbContext(options)
{
    public DbSet<CandidateEntity> Candidates =>
        Set<CandidateEntity>();

    public DbSet<CandidateDocumentEntity> CandidateDocuments =>
    Set<CandidateDocumentEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CandidateDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}