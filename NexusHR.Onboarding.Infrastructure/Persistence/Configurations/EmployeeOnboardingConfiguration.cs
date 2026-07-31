using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeOnboardingConfiguration
    : IEntityTypeConfiguration<EmployeeOnboarding>
{
    public void Configure(
        EntityTypeBuilder<EmployeeOnboarding> builder)
    {
        builder.ToTable("EmployeeOnboardings");

        builder.HasKey(onboarding => onboarding.Id);

        builder.Property(onboarding => onboarding.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(onboarding => onboarding.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(onboarding => onboarding.Email)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(onboarding => onboarding.PositionTitle)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(onboarding => onboarding.Department)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(onboarding => onboarding.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(onboarding => onboarding.HiringProcessId)
            .IsUnique();

        builder.HasIndex(onboarding => onboarding.CandidateId);

        var tasksNavigation = builder
            .HasMany(onboarding => onboarding.Tasks)
            .WithOne()
            .HasForeignKey(task => task.EmployeeOnboardingId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata;

        tasksNavigation.SetPropertyAccessMode(
            PropertyAccessMode.Field);
    }
}
