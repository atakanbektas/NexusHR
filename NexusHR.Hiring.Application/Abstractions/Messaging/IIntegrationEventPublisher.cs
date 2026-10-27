namespace NexusHR.Hiring.Application.Abstractions.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TIntegrationEvent>(
        TIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
        where TIntegrationEvent : class;
}