using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.Onboarding.Application.Abstractions.Persistence;
using NexusHR.Onboarding.Infrastructure.Messaging.Consumers;
using NexusHR.Onboarding.Infrastructure.Persistence;
using NexusHR.Onboarding.Infrastructure.Repositories;

namespace NexusHR.Onboarding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                "OnboardingDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "OnboardingDatabase connection string bulunamadı.");
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

        services.AddDbContext<OnboardingDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddMassTransit(configurator =>
        {
            configurator.SetEndpointNameFormatter(
                new KebabCaseEndpointNameFormatter(
                    "nexushr-onboarding",
                    false));

            configurator.AddConsumer<
                HiringOfferAcceptedConsumer>();

            configurator
                .AddEntityFrameworkOutbox<OnboardingDbContext>(
                    outbox =>
                    {
                        outbox.UsePostgres();
                        outbox.UseBusOutbox();
                    });

            configurator.AddConfigureEndpointsCallback(
                (context, _, endpoint) =>
                {
                    endpoint.UseMessageRetry(
                        retry => retry.Intervals(
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(15)));

                    endpoint
                        .UseEntityFrameworkOutbox<
                            OnboardingDbContext>(context);
                });

            configurator.UsingRabbitMq(
                (context, rabbitMq) =>
                {
                    rabbitMq.Host(
                        rabbitMqHost,
                        rabbitMqVirtualHost,
                        host =>
                        {
                            host.Username(rabbitMqUsername);
                            host.Password(rabbitMqPassword);
                        });

                    rabbitMq.ConfigureEndpoints(context);
                });
        });

        services.AddScoped<
            IEmployeeOnboardingRepository,
            EmployeeOnboardingRepository>();

        services.AddScoped<IUnitOfWork>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    OnboardingDbContext>());

        return services;
    }
}
