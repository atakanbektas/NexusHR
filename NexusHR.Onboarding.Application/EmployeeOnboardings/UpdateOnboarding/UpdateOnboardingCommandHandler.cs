using MediatR;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Contracts.Onboarding;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings.UpdateOnboarding;

internal sealed class UpdateOnboardingCommandHandler(
    IEmployeeOnboardingRepository repository, IUnitOfWork unitOfWork, IIntegrationEventPublisher events)
    : IRequestHandler<UpdateOnboardingCommand>
{
    public async Task Handle(UpdateOnboardingCommand request, CancellationToken cancellationToken)
    {
        var onboarding = await repository.GetByIdAsync(request.OnboardingId, cancellationToken)
            ?? throw new KeyNotFoundException("Onboarding not found.");
        if (request.TaskId is { } taskId)
            onboarding.UpdateTask(taskId, request.TaskStatus!.Value, DateTime.UtcNow);
        else
        {
            if (onboarding.Status == EmployeeOnboardingStatus.Completed) return;
            onboarding.Complete(DateTime.UtcNow);
            await events.PublishAsync(new EmployeeOnboardingCompletedIntegrationEvent(
                Guid.NewGuid(), onboarding.HiringProcessId, onboarding.CandidateId, onboarding.Id, DateTime.UtcNow), cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
