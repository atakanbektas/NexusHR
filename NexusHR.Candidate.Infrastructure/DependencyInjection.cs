using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using NexusHR.Candidate.Application.Abstractions.Documents;
using NexusHR.Candidate.Application.Abstractions.Messaging;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Infrastructure.Documents;
using NexusHR.Candidate.Infrastructure.Messaging;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Candidate.Infrastructure.Repositories;
using NexusHR.Candidate.Infrastructure.Storage;

namespace NexusHR.Candidate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                "CandidateDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CandidateDatabase connection string bulunamadı.");
        }

        var minioSection =
            configuration.GetSection("Minio");

        var minioOptions = new MinioStorageOptions
        {
            Endpoint = minioSection["Endpoint"]
                ?? throw new InvalidOperationException(
                    "Minio:Endpoint ayarı bulunamadı."),

            AccessKey = minioSection["AccessKey"]
                ?? throw new InvalidOperationException(
                    "Minio:AccessKey ayarı bulunamadı."),

            SecretKey = minioSection["SecretKey"]
                ?? throw new InvalidOperationException(
                    "Minio:SecretKey ayarı bulunamadı."),

            BucketName = minioSection["BucketName"]
                ?? throw new InvalidOperationException(
                    "Minio:BucketName ayarı bulunamadı."),

            UseSsl = bool.TryParse(
                minioSection["UseSsl"],
                out var useSsl) && useSsl
        };

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

        services.AddDbContext<CandidateDbContext>(
            options =>
                options.UseNpgsql(connectionString));

        services.AddMassTransit(configurator =>
        {
            configurator
                .AddEntityFrameworkOutbox<CandidateDbContext>(
                    outbox =>
                    {
                        outbox.UsePostgres();
                        outbox.UseBusOutbox();
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

        services.AddSingleton(minioOptions);

        services.AddMinio(configureClient =>
        {
            configureClient
                .WithEndpoint(minioOptions.Endpoint)
                .WithCredentials(
                    minioOptions.AccessKey,
                    minioOptions.SecretKey)
                .WithSSL(minioOptions.UseSsl)
                .Build();
        });

        services.AddSingleton<
            ICandidateDocumentStorage,
            MinioCandidateDocumentStorage>();

        services.AddScoped<
            ICandidateRepository,
            CandidateRepository>();

        services.AddScoped<
            ICandidateDocumentRepository,
            CandidateDocumentRepository>();

        services.AddScoped<
            ICvInformationExtractor,
            PdfCvInformationExtractor>();

        services.AddScoped<
            IIntegrationEventPublisher,
            MassTransitIntegrationEventPublisher>();

        return services;
    }
}