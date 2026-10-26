using FluentValidation;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings.UpdateOnboarding;

public sealed class UpdateOnboardingValidator : AbstractValidator<UpdateOnboardingCommand>
{
    public UpdateOnboardingValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty();
        When(command => command.TaskId.HasValue, () =>
        {
            RuleFor(command => command.TaskId).NotEqual(Guid.Empty);
            RuleFor(command => command.TaskStatus).Must(status =>
                status is OnboardingTaskStatus.InProgress or OnboardingTaskStatus.Completed);
        });
        When(command => !command.TaskId.HasValue, () => RuleFor(command => command.TaskStatus).Null());
    }
}
