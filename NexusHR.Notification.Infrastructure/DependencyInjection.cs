using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.Notification.Application.Abstractions.Email;
using NexusHR.Notification.Infrastructure.Email;
using NexusHR.Notification.Infrastructure
    .Messaging.Consumers;

namespace NexusHR.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(
            configuration.GetSection(
                SmtpOptions.SectionName));

        services.AddScoped<
            IEmailSender,
            MailKitEmailSender>();

        var rabbitMqSection =
            configuration.GetSection(
                "RabbitMq");

        var rabbitMqHost =
            rabbitMqSection["Host"]
            ?? throw new InvalidOperationException(
                "RabbitMq:Host ayarı bulunamadı.");

        var rabbitMqVirtualHost =
            rabbitMqSection["VirtualHost"]
            ?? "/";

        var rabbitMqUsername =
            rabbitMqSection["Username"]
            ?? throw new InvalidOperationException(
                "RabbitMq:Username ayarı bulunamadı.");

        var rabbitMqPassword =
            rabbitMqSection["Password"]
            ?? throw new InvalidOperationException(
                "RabbitMq:Password ayarı bulunamadı.");

        services.AddMassTransit(
            configurator =>
            {
                configurator
                    .SetEndpointNameFormatter(
                        new KebabCaseEndpointNameFormatter(
                            "nexushr-notification",
                            false));

                configurator.AddConsumer<
                    HiringOfferSentConsumer>();

                configurator
                    .AddConfigureEndpointsCallback(
                        (_, _, endpoint) =>
                        {
                            endpoint.UseMessageRetry(
                                retry =>
                                    retry.Intervals(
                                        TimeSpan.FromSeconds(1),
                                        TimeSpan.FromSeconds(5),
                                        TimeSpan.FromSeconds(15)));
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

        return services;
    }
}