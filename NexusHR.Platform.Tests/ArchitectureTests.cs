namespace NexusHR.Platform.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void BusinessDomainsHaveNoInfrastructureApplicationOrHttpDependencies()
    {
        foreach (var type in new[] { typeof(Candidate.Domain.Candidates.Candidate), typeof(Hiring.Domain.HiringProcesses.HiringProcess), typeof(Onboarding.Domain.EmployeeOnboardings.EmployeeOnboarding) })
        {
            var references = type.Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
            Assert.DoesNotContain(references, name => name.Contains("Infrastructure", StringComparison.Ordinal) || name.Contains("Application", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || name.StartsWith("MassTransit", StringComparison.Ordinal) || name.StartsWith("MediatR", StringComparison.Ordinal));
        }
    }
}
