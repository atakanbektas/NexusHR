using Microsoft.EntityFrameworkCore;
using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Infrastructure.Persistence;

public sealed class CandidateDbContext(
    DbContextOptions<CandidateDbContext> options)
    : DbContext(options)
{
    public DbSet<CandidateEntity> Candidates =>
        Set<CandidateEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CandidateDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}