using MediatR;
using NexusHR.Onboarding.Application.Abstractions.Persistence;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings
    .GetEmployeeOnboardingById;

internal sealed class GetEmployeeOnboardingByIdQueryHandler(
    IEmployeeOnboardingRepository repository)
    : IRequestHandler<
        GetEmployeeOnboardingByIdQuery,
        EmployeeOnboardingDetailResponse?>
{
    public async Task<EmployeeOnboardingDetailResponse?> Handle(
        GetEmployeeOnboardingByIdQuery request,
        CancellationToken cancellationToken)
    {
        var onboarding = await repository.GetByIdAsync(
            request.OnboardingId,
            cancellationToken);

        if (onboarding is null)
        {
            return null;
        }

        return new EmployeeOnboardingDetailResponse(
            onboarding.Id,
            onboarding.HiringProcessId,
            onboarding.CandidateId,
            onboarding.FirstName,
            onboarding.LastName,
            onboarding.Email,
            onboarding.PositionTitle,
            onboarding.Department,
            onboarding.ProposedStartDate,
            onboarding.Status.ToString(),
            onboarding.CreatedAtUtc,
            onboarding.Tasks
                .OrderBy(task => task.DueDate)
                .ThenBy(task => task.Title)
                .Select(task =>
                    new OnboardingTaskResponse(
                        task.Id,
                        task.Title,
                        task.Type.ToString(),
                        task.AssignedDepartment,
                        task.Status.ToString(),
                        task.DueDate,
                        task.CreatedAtUtc,
                        task.CompletedAtUtc))
                .ToArray());
    }
}
