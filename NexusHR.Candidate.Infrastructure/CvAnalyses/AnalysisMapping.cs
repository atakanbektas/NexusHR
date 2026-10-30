using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Domain.CvAnalyses;

namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

internal sealed class AnalysisMapping : IEntityTypeConfiguration<CvAnalysis>, IEntityTypeConfiguration<CvAnalysisAttempt>,
    IEntityTypeConfiguration<CvAnalysisReview>, IEntityTypeConfiguration<AiDailyBudget>, IEntityTypeConfiguration<AiDailyQuota>
{
    public void Configure(EntityTypeBuilder<CvAnalysis> e)
    {
        e.ToTable("cv_analyses"); e.HasKey(x => x.Id);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        e.Property(x => x.Version).IsRowVersion();
        e.Property(x => x.RequestedBy).HasMaxLength(200); e.Property(x => x.IdempotencyKey).HasMaxLength(100);
        e.Property(x => x.Model).HasMaxLength(200); e.Property(x => x.Provider).HasMaxLength(50);
        e.Property(x => x.ContentHash).HasMaxLength(64); e.Property(x => x.ErrorCode).HasMaxLength(100);
        e.Property(x => x.ResultJson).HasColumnType("jsonb"); e.Property(x => x.ReservedUsd).HasPrecision(18, 8);
        e.HasIndex(x => new { x.CandidateId, x.RequestedBy, x.IdempotencyKey }).IsUnique();
        e.HasIndex(x => x.DocumentId).IsUnique().HasFilter("\"Status\" IN ('Queued', 'Running')");
        e.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        e.HasOne<CandidateDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<CvAnalysisAttempt> e)
    {
        e.ToTable("cv_analysis_attempts"); e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.AnalysisId, x.Number }).IsUnique();
        e.Property(x => x.ReservedUsd).HasPrecision(18, 8); e.Property(x => x.UsageCostUsd).HasPrecision(18, 8);
        e.Property(x => x.InputRate).HasPrecision(18, 8); e.Property(x => x.OutputRate).HasPrecision(18, 8);
        e.HasOne<CvAnalysis>().WithMany().HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<CvAnalysisReview> e)
    {
        e.ToTable("cv_analysis_reviews"); e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.AnalysisId, x.Revision }).IsUnique();
        e.Property(x => x.Notes).HasMaxLength(4000); e.Property(x => x.Decision).HasMaxLength(20);
        e.HasOne<CvAnalysis>().WithMany().HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<AiDailyBudget> e)
    {
        e.ToTable("ai_daily_budgets"); e.HasKey(x => x.Day); e.Property(x => x.CommittedUsd).HasPrecision(18, 8);
    }
    public void Configure(EntityTypeBuilder<AiDailyQuota> e)
    {
        e.ToTable("ai_daily_quotas"); e.HasKey(x => new { x.Day, x.Subject }); e.Property(x => x.Subject).HasMaxLength(200);
    }
}
