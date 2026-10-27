using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace NexusHR.Hiring.Infrastructure.Persistence;
public sealed class HiringDesignTimeFactory : IDesignTimeDbContextFactory<HiringDbContext>
{
    public HiringDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<HiringDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__HiringDatabase")
                ?? "Host=localhost;Database=nexushr_Hiring")
            .Options);
}
