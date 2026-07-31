using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Application.Abstractions.Persistence;

public interface IEmployeeOnboardingRepository
{
    Task<EmployeeOnboarding?> GetByHiringProcessIdAsync(
        Guid hiringProcessId,
        CancellationToken cancellationToken);

    Task<EmployeeOnboarding?> GetByIdAsync(
        Guid onboardingId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EmployeeOnboarding>> GetAllAsync(
        CancellationToken cancellationToken);

    Task AddAsync(
        EmployeeOnboarding onboarding,
        CancellationToken cancellationToken);
}
