using Microsoft.EntityFrameworkCore;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;
using NexusHR.Onboarding.Infrastructure.Persistence;

namespace NexusHR.Onboarding.Infrastructure.Repositories;

internal sealed class EmployeeOnboardingRepository(
    OnboardingDbContext dbContext)
    : IEmployeeOnboardingRepository
{
    public async Task<EmployeeOnboarding?>
        GetByHiringProcessIdAsync(
            Guid hiringProcessId,
            CancellationToken cancellationToken)
    {
        return await dbContext.EmployeeOnboardings
            .Include(onboarding => onboarding.Tasks)
            .SingleOrDefaultAsync(
                onboarding =>
                    onboarding.HiringProcessId ==
                    hiringProcessId,
                cancellationToken);
    }

    public async Task<EmployeeOnboarding?> GetByIdAsync(
        Guid onboardingId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EmployeeOnboardings
            .AsNoTracking()
            .Include(onboarding => onboarding.Tasks)
            .SingleOrDefaultAsync(
                onboarding => onboarding.Id == onboardingId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmployeeOnboarding>>
        GetAllAsync(
            CancellationToken cancellationToken)
    {
        return await dbContext.EmployeeOnboardings
            .AsNoTracking()
            .Include(onboarding => onboarding.Tasks)
            .OrderByDescending(onboarding =>
                onboarding.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task AddAsync(
        EmployeeOnboarding onboarding,
        CancellationToken cancellationToken)
    {
        await dbContext.EmployeeOnboardings.AddAsync(
            onboarding,
            cancellationToken);
    }
}
