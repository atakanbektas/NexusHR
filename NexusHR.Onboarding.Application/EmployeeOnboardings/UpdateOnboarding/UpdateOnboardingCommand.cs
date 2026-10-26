using MediatR;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings.UpdateOnboarding;

public sealed record UpdateOnboardingCommand(
    Guid OnboardingId, Guid? TaskId, OnboardingTaskStatus? TaskStatus) : IRequest;
