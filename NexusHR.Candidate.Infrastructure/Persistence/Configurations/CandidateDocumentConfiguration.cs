using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;
using CandidateDocumentEntity =
    NexusHR.Candidate.Domain.Candidates.CandidateDocument;

namespace NexusHR.Candidate.Infrastructure.Persistence.Configurations;

internal sealed class CandidateDocumentConfiguration
    : IEntityTypeConfiguration<CandidateDocumentEntity>
{
    public void Configure(
        EntityTypeBuilder<CandidateDocumentEntity> builder)
    {
        builder.ToTable("candidate_documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.CandidateId)
            .IsRequired();

        builder.Property(document => document.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(document => document.ObjectName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(document => document.SizeBytes)
            .IsRequired();

        builder.Property(document => document.UploadedAtUtc)
            .IsRequired();

        builder.HasIndex(document => document.ObjectName)
            .IsUnique();

        builder.HasIndex(document => new
        {
            document.CandidateId,
            document.Type
        })
            .IsUnique();

        builder.HasOne<CandidateEntity>()
            .WithMany()
            .HasForeignKey(document => document.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}