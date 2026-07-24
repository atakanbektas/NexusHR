using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusHR.Hiring.Domain.EligibleCandidates;
using NexusHR.Hiring.Domain.HiringProcesses;
using HiringProcessEntity =
    NexusHR.Hiring.Domain.HiringProcesses.HiringProcess;

namespace NexusHR.Hiring.Infrastructure.Persistence.Configurations;

internal sealed class HiringProcessConfiguration
    : IEntityTypeConfiguration<HiringProcessEntity>
{
    public void Configure(
        EntityTypeBuilder<HiringProcessEntity> builder)
    {
        builder.ToTable("hiring_processes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CandidateId)
            .IsRequired();

        builder.Property(x => x.PositionTitle)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Department)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.EmploymentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.GrossSalary)
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .HasMaxLength(3);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<EligibleCandidate>()
            .WithMany()
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CandidateId)
            .IsUnique()
            .HasDatabaseName(
                "ux_hiring_processes_candidate_active")
            .HasFilter(
                "\"Status\" IN ('Draft', 'OfferPrepared', 'OfferSent', 'OfferAccepted')");
    }
}