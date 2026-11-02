using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace NexusHR.Onboarding.Tests;

public sealed class BrokerRecoveryTests
{
    [Fact]
    public async Task ColdNodeBackupRestoresTopologyAndPersistentMessageOnAnIndependentSameNamedNode()
    {
        var password = Guid.NewGuid().ToString("N"); var cookie = Guid.NewGuid().ToString("N");
        ContainerBuilder Node() => new ContainerBuilder("rabbitmq:4.3.3-management").WithHostname("recovery-node")
            .WithEnvironment("RABBITMQ_NODENAME", "rabbit@recovery-node").WithEnvironment("RABBITMQ_ERLANG_COOKIE", cookie)
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "recovery-fixture").WithEnvironment("RABBITMQ_DEFAULT_PASS", password)
            .WithPortBinding(15672, true).WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x =>
                x.ForPort(15672).ForPath("/api/overview").WithBasicAuthentication("recovery-fixture", password)));
        await using var source = Node().Build(); await source.StartAsync();
        using var original = Client(source, password);
        (await original.PutAsJsonAsync("api/queues/%2F/recovery-fixture", new { durable = true, auto_delete = false, arguments = new Dictionary<string, string>() })).EnsureSuccessStatusCode();
        var published = await original.PostAsJsonAsync("api/exchanges/%2F/amq.default/publish", new {
            properties = new { delivery_mode = 2 }, routing_key = "recovery-fixture", payload = "synthetic-persistent-recovery-message", payload_encoding = "string" });
        published.EnsureSuccessStatusCode(); Assert.True((await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("routed").GetBoolean());
        // Quiesced disposable source. Restoring definitions alone would lose this message.
        Assert.Equal(0, (await source.ExecAsync(["rabbitmqctl", "stop_app"])).ExitCode);
        Assert.Equal(0, (await source.ExecAsync(["tar", "czf", "/tmp/node-backup.tgz", "-C", "/var/lib/rabbitmq", "mnesia", ".erlang.cookie"])).ExitCode);
        var archive = await source.ReadFileAsync("/tmp/node-backup.tgz"); Assert.NotEmpty(archive);
        await using var target = Node().WithResourceMapping(archive, "/tmp/node-backup.tgz").WithCreateParameterModifier(p => p.User = "0")
            .WithEntrypoint("/bin/sh", "-ec").WithCommand("tar xzf /tmp/node-backup.tgz -C /var/lib/rabbitmq && exec docker-entrypoint.sh rabbitmq-server").Build();
        await target.StartAsync(); using var restored = Client(target, password);
        var read = await restored.PostAsJsonAsync("api/queues/%2F/recovery-fixture/get", new { count = 1, ackmode = "ack_requeue_true", encoding = "auto", truncate = 1000 });
        read.EnsureSuccessStatusCode(); var messages = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, messages.GetArrayLength()); Assert.Equal("synthetic-persistent-recovery-message", messages[0].GetProperty("payload").GetString());
    }
    private static HttpClient Client(IContainer node, string password)
    {
        var client = new HttpClient { BaseAddress = new Uri($"http://{node.Hostname}:{node.GetMappedPublicPort(15672)}/"), Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"recovery-fixture:{password}")));
        return client;
    }
}
