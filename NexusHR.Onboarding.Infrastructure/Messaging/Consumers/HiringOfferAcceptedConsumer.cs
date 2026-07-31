using MassTransit;
using NexusHR.Contracts.Hiring;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Infrastructure.Messaging.Consumers;

internal sealed class HiringOfferAcceptedConsumer(
    IEmployeeOnboardingRepository repository,
    IUnitOfWork unitOfWork)
    : IConsumer<HiringOfferAcceptedIntegrationEvent>
{
    public async Task Consume(
        ConsumeContext<HiringOfferAcceptedIntegrationEvent> context)
    {
        var message = context.Message;

        if (message.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Desteklenmeyen HiringOfferAccepted event versiyonu: {message.SchemaVersion}");
        }

        var existingOnboarding =
            await repository.GetByHiringProcessIdAsync(
                message.HiringProcessId,
                context.CancellationToken);

        if (existingOnboarding is not null)
        {
            return;
        }

        var onboarding = new EmployeeOnboarding(
            Guid.NewGuid(),
            message.HiringProcessId,
            message.CandidateId,
            message.CandidateFirstName,
            message.CandidateLastName,
            message.CandidateEmail,
            message.PositionTitle,
            message.Department,
            message.ProposedStartDate,
            message.OccurredAtUtc);

        await repository.AddAsync(
            onboarding,
            context.CancellationToken);

        await unitOfWork.SaveChangesAsync(
            context.CancellationToken);
    }
}
