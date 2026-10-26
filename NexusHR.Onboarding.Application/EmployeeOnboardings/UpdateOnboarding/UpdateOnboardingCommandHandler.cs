using MediatR;
using NexusHR.Onboarding.Application.Abstractions.Persistence;

namespace NexusHR.Onboarding.Application.EmployeeOnboardings.UpdateOnboarding;

internal sealed class UpdateOnboardingCommandHandler(
    IEmployeeOnboardingRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateOnboardingCommand>
{
    public async Task Handle(UpdateOnboardingCommand request, CancellationToken cancellationToken)
    {
        var onboarding = await repository.GetByIdAsync(request.OnboardingId, cancellationToken)
            ?? throw new KeyNotFoundException("Onboarding not found.");
        if (request.TaskId is { } taskId)
            onboarding.UpdateTask(taskId, request.TaskStatus!.Value, DateTime.UtcNow);
        else
            onboarding.Complete(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
