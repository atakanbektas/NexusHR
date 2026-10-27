using MediatR;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings
    .GetEmployeeOnboardings;

internal sealed class GetEmployeeOnboardingsQueryHandler(
    IEmployeeOnboardingRepository repository)
    : IRequestHandler<
        GetEmployeeOnboardingsQuery,
        IReadOnlyCollection<
            EmployeeOnboardingListItemResponse>>
{
    public async Task<IReadOnlyCollection<
        EmployeeOnboardingListItemResponse>> Handle(
        GetEmployeeOnboardingsQuery request,
        CancellationToken cancellationToken)
    {
        var onboardings = await repository.GetAllAsync(
            cancellationToken);

        return onboardings
            .Select(onboarding =>
                new EmployeeOnboardingListItemResponse(
                    onboarding.Id,
                    onboarding.HiringProcessId,
                    onboarding.CandidateId,
                    $"{onboarding.FirstName} {onboarding.LastName}",
                    onboarding.Email,
                    onboarding.PositionTitle,
                    onboarding.Department,
                    onboarding.ProposedStartDate,
                    onboarding.Status.ToString(),
                    onboarding.Tasks.Count,
                    onboarding.Tasks.Count(task =>
                        task.Status ==
                        OnboardingTaskStatus.Pending),
                    onboarding.CreatedAtUtc))
            .ToArray();
    }
}
