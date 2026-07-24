using MassTransit;
using Microsoft.EntityFrameworkCore;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Domain.EligibleCandidates;
using HiringProcessEntity =
    NexusHR.Hiring.Domain.HiringProcesses.HiringProcess;

namespace NexusHR.Hiring.Infrastructure.Persistence;

public sealed class HiringDbContext(
    DbContextOptions<HiringDbContext> options)
    : DbContext(options),
      IUnitOfWork
{
    public DbSet<HiringProcessEntity> HiringProcesses =>
        Set<HiringProcessEntity>();

    public DbSet<EligibleCandidate> EligibleCandidates =>
        Set<EligibleCandidate>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HiringDbContext).Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}