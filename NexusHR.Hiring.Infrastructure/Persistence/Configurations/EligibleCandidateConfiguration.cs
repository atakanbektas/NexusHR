using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusHR.Hiring.Domain.EligibleCandidates;

namespace NexusHR.Hiring.Infrastructure.Persistence.Configurations;

internal sealed class EligibleCandidateConfiguration
    : IEntityTypeConfiguration<EligibleCandidate>
{
    public void Configure(
        EntityTypeBuilder<EligibleCandidate> builder)
    {
        builder.ToTable("eligible_candidates");

        builder.HasKey(x => x.CandidateId);

        builder.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.BecameEligibleAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Email);
    }
}