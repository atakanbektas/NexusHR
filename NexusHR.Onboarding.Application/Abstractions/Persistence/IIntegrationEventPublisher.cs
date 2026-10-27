namespace NexusHR.Onboarding.Application.Abstractions.Persistence;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class;
}
