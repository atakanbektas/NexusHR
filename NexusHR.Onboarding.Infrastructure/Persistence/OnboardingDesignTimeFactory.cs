using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexusHR.Onboarding.Infrastructure.Persistence;

public sealed class OnboardingDesignTimeFactory : IDesignTimeDbContextFactory<OnboardingDbContext>
{
    public OnboardingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<OnboardingDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__OnboardingDatabase")
                ?? "Host=localhost;Database=nexushr_onboarding")
            .Options);
}
