using MassTransit;
using NexusHR.Candidate.Application.Abstractions.Messaging;

namespace NexusHR.Candidate.Infrastructure.Messaging;

internal sealed class MassTransitIntegrationEventPublisher(
    IPublishEndpoint publishEndpoint)
    : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken)
        where T : class
    {
        return publishEndpoint.Publish(
            integrationEvent,
            cancellationToken);
    }
}