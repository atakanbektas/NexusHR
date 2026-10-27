using MediatR;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings
    .GetEmployeeOnboardings;

public sealed record GetEmployeeOnboardingsQuery
    : IRequest<IReadOnlyCollection<
        EmployeeOnboardingListItemResponse>>;
