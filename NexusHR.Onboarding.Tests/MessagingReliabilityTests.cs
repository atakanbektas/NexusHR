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
        // A fresh broker has no durable subscription until the receive endpoint is ready.
        // Publishing immediately after the default asynchronous host start can lose the first event.
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
        });
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

            // Unsupported messages must exhaust bounded retries into _error, not commit partial state.
            var poison = message with { EventId = Guid.NewGuid(), HiringProcessId = Guid.NewGuid(), SchemaVersion = 999 };
            await bus.Publish(poison, context => context.MessageId = poison.EventId);
            var errorDeadline = DateTime.UtcNow.AddSeconds(50);
            while (true)
            {
                var queues = await broker.ExecAsync(["rabbitmqctl", "list_queues", "--formatter=json", "name", "messages_ready"]);
                Assert.Equal(0, queues.ExitCode);
                using var parsed = System.Text.Json.JsonDocument.Parse(queues.Stdout);
                if (parsed.RootElement.EnumerateArray().Any(q => q.GetProperty("name").GetString()!.EndsWith("_error", StringComparison.Ordinal) && q.GetProperty("messages_ready").GetInt32() == 1)) break;
                Assert.True(DateTime.UtcNow < errorDeadline, "Poison message never reached its durable error queue.");
                await Task.Delay(500);
            }
            await using (var scope = host.Services.CreateAsyncScope())
                Assert.False(await scope.ServiceProvider.GetRequiredService<OnboardingDbContext>().EmployeeOnboardings.AnyAsync(x => x.HiringProcessId == poison.HiringProcessId));
            var afterPoison = message with { EventId = Guid.NewGuid(), HiringProcessId = Guid.NewGuid(), CandidateId = Guid.NewGuid() };
            await bus.Publish(afterPoison, context => context.MessageId = afterPoison.EventId);
            await Eventually(db => db.EmployeeOnboardings.AnyAsync(x => x.HiringProcessId == afterPoison.HiringProcessId));
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
