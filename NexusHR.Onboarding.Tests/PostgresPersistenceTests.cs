using Microsoft.EntityFrameworkCore;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;
using NexusHR.Onboarding.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexusHR.Onboarding.Tests;

public sealed class PostgresPersistenceTests
{
    [Fact]
    public async Task MigrationPersistsTasksAndRejectsDuplicateHiringProcessesAndConcurrentChanges()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<OnboardingDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var first = new OnboardingDbContext(options);
        await first.Database.MigrateAsync();
        var item = OnboardingWorkflowTests.Create();
        first.Add(item); await first.SaveChangesAsync(); first.ChangeTracker.Clear();
        await using var second = new OnboardingDbContext(options);
        var a = await first.EmployeeOnboardings.Include(row => row.Tasks).SingleAsync();
        var b = await second.EmployeeOnboardings.Include(row => row.Tasks).SingleAsync();
        Assert.Equal(3, a.Tasks.Count);
        a.UpdateTask(a.Tasks.First().Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow);
        await first.SaveChangesAsync();
        b.UpdateTask(b.Tasks.Last().Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        second.Add(new EmployeeOnboarding(Guid.NewGuid(), a.HiringProcessId, a.CandidateId, "Ada", "Lovelace",
            "ada@example.test", "Engineer", "IT", a.ProposedStartDate, DateTime.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }
}
