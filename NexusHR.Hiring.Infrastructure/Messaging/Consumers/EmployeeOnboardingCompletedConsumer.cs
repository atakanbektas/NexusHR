using MassTransit;
using NexusHR.Contracts.Onboarding;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.Infrastructure.Messaging.Consumers;

internal sealed class EmployeeOnboardingCompletedConsumer(IHiringProcessRepository repository, IUnitOfWork unitOfWork)
    : IConsumer<EmployeeOnboardingCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<EmployeeOnboardingCompletedIntegrationEvent> context)
    {
        var message = context.Message;
        if (message.SchemaVersion != 1) throw new InvalidOperationException("Unsupported onboarding event schema.");
        var process = await repository.GetByIdAsync(message.HiringProcessId, context.CancellationToken)
            ?? throw new InvalidOperationException("Hiring process has not been found.");
        if (process.CandidateId != message.CandidateId) throw new InvalidOperationException("Candidate identity does not match.");
        if (process.Status == HiringProcessStatus.Completed && process.EmployeeId == message.EmployeeId) return;
        process.Complete(message.EmployeeId);
        await repository.UpdateAsync(process, context.CancellationToken);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
