using DotNet.Testcontainers.Builders;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexusHR.Contracts.Hiring;
using NexusHR.Onboarding.Application;
using NexusHR.Onboarding.Application.EmployeeOnboardings.UpdateOnboarding;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;
using NexusHR.Onboarding.Infrastructure;
using NexusHR.Onboarding.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexusHR.Onboarding.Tests;

public sealed class MessagingReliabilityTests
{
    [Fact]
    public async Task InboxDeduplicatesAndOutboxSurvivesBrokerOutage()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await using var broker = new ContainerBuilder("rabbitmq:4.3.3-management")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "test-user")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "test-password")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672)).Build();
        await Task.WhenAll(postgres.StartAsync(), broker.StartAsync());
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OnboardingDatabase"] = postgres.GetConnectionString(),
            ["RabbitMq:Host"] = broker.Hostname, ["RabbitMq:Port"] = broker.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "test-user", ["RabbitMq:Password"] = "test-password",
        });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        using var host = builder.Build();
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<OnboardingDbContext>().Database.MigrateAsync();
        await host.StartAsync();
        try
        {
            var message = new HiringOfferAcceptedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Ada", "Lovelace", "ada@example.test", "Engineer", "IT", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), DateTime.UtcNow);
            var bus = host.Services.GetRequiredService<IBus>();
            await bus.Publish(message, context => context.MessageId = message.EventId);
            await bus.Publish(message, context => context.MessageId = message.EventId);
            var duplicate = message with { EventId = Guid.NewGuid() };
            await bus.Publish(duplicate, context => context.MessageId = duplicate.EventId);
            await Eventually(async db => await db.Set<InboxState>().CountAsync(item => item.Consumed != null) == 2);

            Guid id;
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OnboardingDbContext>();
                var onboarding = await db.EmployeeOnboardings.Include(item => item.Tasks).SingleAsync();
                Assert.Equal(message.HiringProcessId, onboarding.HiringProcessId);
                Assert.Equal(3, onboarding.Tasks.Count);
                id = onboarding.Id;
                foreach (var task in onboarding.Tasks)
                {
                    onboarding.UpdateTask(task.Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow);
                    onboarding.UpdateTask(task.Id, OnboardingTaskStatus.Completed, DateTime.UtcNow);
                }
                await db.SaveChangesAsync();
            }
            // Stop the broker application, keeping Docker's random host-port mapping stable.
            Assert.Equal(0, (await broker.ExecAsync(["rabbitmqctl", "stop_app"])).ExitCode);
            await using (var scope = host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateOnboardingCommand(id, null, null));
                var db = scope.ServiceProvider.GetRequiredService<OnboardingDbContext>();
                Assert.Equal(EmployeeOnboardingStatus.Completed, (await db.EmployeeOnboardings.FindAsync(id))!.Status);
                Assert.True(await db.Set<OutboxMessage>().AnyAsync());
            }
            Assert.Equal(0, (await broker.ExecAsync(["rabbitmqctl", "start_app"])).ExitCode);
            await Eventually(async db => !await db.Set<OutboxMessage>().AnyAsync());
        }
        finally { await host.StopAsync(); }

        async Task Eventually(Func<OnboardingDbContext, Task<bool>> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (!timeout.IsCancellationRequested)
            {
                await using var scope = host.Services.CreateAsyncScope();
                if (await condition(scope.ServiceProvider.GetRequiredService<OnboardingDbContext>())) return;
                await Task.Delay(250, timeout.Token);
            }
            throw new TimeoutException("Messaging did not reach the expected durable state.");
        }
    }
}
