using MassTransit;
using NexusHR.Hiring.Application.Abstractions.Messaging;

namespace NexusHR.Hiring.Infrastructure.Messaging;

internal sealed class MassTransitIntegrationEventPublisher(
    IPublishEndpoint publishEndpoint)
    : IIntegrationEventPublisher
{
    public Task PublishAsync<TIntegrationEvent>(
        TIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
        where TIntegrationEvent : class
    {
        return publishEndpoint.Publish(
            integrationEvent,
            cancellationToken);
    }
}