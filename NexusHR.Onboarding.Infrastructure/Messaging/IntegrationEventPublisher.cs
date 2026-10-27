using MassTransit;
using NexusHR.Onboarding.Application.Abstractions.Persistence;

namespace NexusHR.Onboarding.Infrastructure.Messaging;

internal sealed class IntegrationEventPublisher(IPublishEndpoint endpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class =>
        endpoint.Publish(message, cancellationToken);
}
