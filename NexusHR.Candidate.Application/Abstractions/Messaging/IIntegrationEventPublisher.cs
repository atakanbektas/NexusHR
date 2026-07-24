namespace NexusHR.Candidate.Application.Abstractions.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken)
        where T : class;
}