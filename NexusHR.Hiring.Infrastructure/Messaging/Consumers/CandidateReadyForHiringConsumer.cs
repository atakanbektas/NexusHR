using MassTransit;
using NexusHR.Contracts.Candidates;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Domain.EligibleCandidates;

namespace NexusHR.Hiring.Infrastructure.Messaging.Consumers;

internal sealed class CandidateReadyForHiringConsumer(
    IEligibleCandidateRepository eligibleCandidateRepository,
    IUnitOfWork unitOfWork)
    : IConsumer<CandidateReadyForHiringIntegrationEvent>
{
    public async Task Consume(
        ConsumeContext<CandidateReadyForHiringIntegrationEvent> context)
    {
        var message = context.Message;

        if (message.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Desteklenmeyen CandidateReadyForHiring event versiyonu: {message.SchemaVersion}");
        }

        var existingCandidate =
            await eligibleCandidateRepository.GetByCandidateIdAsync(
                message.CandidateId,
                context.CancellationToken);

        if (existingCandidate is not null)
        {
            return;
        }

        var eligibleCandidate = new EligibleCandidate(
            message.CandidateId,
            message.FirstName,
            message.LastName,
            message.Email,
            message.OccurredAtUtc);

        await eligibleCandidateRepository.AddAsync(
            eligibleCandidate,
            context.CancellationToken);

        await unitOfWork.SaveChangesAsync(
            context.CancellationToken);
    }
}