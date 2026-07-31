using MassTransit;
using Microsoft.EntityFrameworkCore;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Infrastructure.Persistence;

public sealed class OnboardingDbContext : DbContext, IUnitOfWork
{
    public OnboardingDbContext(
        DbContextOptions<OnboardingDbContext> options)
        : base(options)
    {
    }

    public DbSet<EmployeeOnboarding> EmployeeOnboardings =>
        Set<EmployeeOnboarding>();

    public DbSet<OnboardingTask> OnboardingTasks =>
        Set<OnboardingTask>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OnboardingDbContext).Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
