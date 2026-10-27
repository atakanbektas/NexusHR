using MassTransit;
using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Infrastructure.Storage;
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

        modelBuilder.Entity<CvUploadIntent>(entity =>
        {
            entity.ToTable("cv_upload_intents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ObjectName).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.ReconcileAfterUtc);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
