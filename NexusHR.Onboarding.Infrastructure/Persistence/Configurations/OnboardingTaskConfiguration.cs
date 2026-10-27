using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingTaskConfiguration
    : IEntityTypeConfiguration<OnboardingTask>
{
    public void Configure(
        EntityTypeBuilder<OnboardingTask> builder)
    {
        builder.ToTable("OnboardingTasks");

        builder.HasKey(task => task.Id);

        builder.Property(task => task.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(task => task.AssignedDepartment)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(task => task.Type)
            .HasConversion<string>()
            .HasMaxLength(75)
            .IsRequired();

        builder.Property(task => task.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(task => task.EmployeeOnboardingId);

        builder.HasIndex(task => new
        {
            task.AssignedDepartment,
            task.Status
        });
    }
}
