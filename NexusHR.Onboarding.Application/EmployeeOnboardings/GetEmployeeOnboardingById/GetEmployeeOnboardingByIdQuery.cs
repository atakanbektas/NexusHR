using MediatR;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings
    .GetEmployeeOnboardingById;

public sealed record GetEmployeeOnboardingByIdQuery(
    Guid OnboardingId)
    : IRequest<EmployeeOnboardingDetailResponse?>;
