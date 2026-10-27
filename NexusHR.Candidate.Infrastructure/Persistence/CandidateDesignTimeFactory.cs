using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace NexusHR.Candidate.Infrastructure.Persistence;
public sealed class CandidateDesignTimeFactory : IDesignTimeDbContextFactory<CandidateDbContext>
{
    public CandidateDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<CandidateDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__CandidateDatabase")
                ?? "Host=localhost;Database=nexushr_Candidate")
            .Options);
}
