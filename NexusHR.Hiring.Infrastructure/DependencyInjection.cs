using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.Hiring.Application.Abstractions.Messaging;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Application.Abstractions.Security;
using NexusHR.Hiring.Infrastructure.Messaging;
using NexusHR.Hiring.Infrastructure.Messaging.Consumers;
using NexusHR.Hiring.Infrastructure.Persistence;
using NexusHR.Hiring.Infrastructure.Repositories;
using NexusHR.Hiring.Infrastructure.Security;

namespace NexusHR.Hiring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                "HiringDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "HiringDatabase connection string bulunamadı.");
        }

        var rabbitMqSection =
            configuration.GetSection("RabbitMq");

        var rabbitMqHost = rabbitMqSection["Host"]
            ?? throw new InvalidOperationException(
                "RabbitMq:Host ayarı bulunamadı.");

        var rabbitMqVirtualHost =
            rabbitMqSection["VirtualHost"] ?? "/";

        var rabbitMqUsername = rabbitMqSection["Username"]
            ?? throw new InvalidOperationException(
                "RabbitMq:Username ayarı bulunamadı.");

        var rabbitMqPassword = rabbitMqSection["Password"]
            ?? throw new InvalidOperationException(
                "RabbitMq:Password ayarı bulunamadı.");

        services.AddDbContext<HiringDbContext>(
            options =>
                options.UseNpgsql(connectionString));

        services.AddMassTransit(configurator =>
        {
            configurator.SetEndpointNameFormatter(
                new KebabCaseEndpointNameFormatter(
                    "nexushr-hiring",
                    false));

            configurator.AddConsumer<
                CandidateReadyForHiringConsumer>();

            configurator
                .AddEntityFrameworkOutbox<HiringDbContext>(
                    outbox =>
                    {
                        outbox.UsePostgres();
                        outbox.UseBusOutbox();
                    });

            configurator.AddConfigureEndpointsCallback(
                (context, _, endpoint) =>
                {
                    endpoint.UseMessageRetry(
                        retry =>
                            retry.Intervals(
                                TimeSpan.FromSeconds(1),
                                TimeSpan.FromSeconds(5),
                                TimeSpan.FromSeconds(15)));

                    endpoint
                        .UseEntityFrameworkOutbox<HiringDbContext>(
                            context);
                });

            configurator.UsingRabbitMq(
                (context, rabbitMq) =>
                {
                    rabbitMq.Host(
                        rabbitMqHost,
                        rabbitMqVirtualHost,
                        host =>
                        {
                            host.Username(
                                rabbitMqUsername);

                            host.Password(
                                rabbitMqPassword);
                        });

                    rabbitMq.ConfigureEndpoints(
                        context);
                });
        });

        services.AddScoped<
            IEligibleCandidateRepository,
            EligibleCandidateRepository>();

        services.AddScoped<
            IHiringProcessRepository,
            HiringProcessRepository>();

        services.AddScoped<IUnitOfWork>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    HiringDbContext>());

        services.AddSingleton<
    IOfferResponseTokenService,
    OfferResponseTokenService>();

        services.AddScoped<
    IIntegrationEventPublisher,
    MassTransitIntegrationEventPublisher>();

        return services;
    }
}