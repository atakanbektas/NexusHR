using System.Net.Http.Headers;
using System.Text;
using DotNet.Testcontainers.Builders;

namespace NexusHR.Onboarding.Tests;

public sealed class FrontendPrivacyTests
{
    [Fact]
    public async Task OfferCapabilitiesAndReferrersNeverEnterFrontendAccessLogs()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "NexusHR.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        await using var nginx = new ContainerBuilder("nginx:alpine")
            .WithResourceMapping(await File.ReadAllBytesAsync(Path.Combine(root.FullName, "NexusHR.Web", "nginx.conf")), "/etc/nginx/conf.d/default.conf")
            .WithResourceMapping(Encoding.UTF8.GetBytes("<!doctype html><title>Synthetic privacy fixture</title>"), "/usr/share/nginx/html/index.html")
            .WithPortBinding(80, true).WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x => x.ForPort(80).ForPath("/"))).Build();
        await nginx.StartAsync(); using var client = new HttpClient { BaseAddress = new Uri($"http://{nginx.Hostname}:{nginx.GetMappedPublicPort(80)}") };
        var secret = "synthetic-sensitive-capability-" + Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/public/offers/respond?token=" + secret);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret); request.Headers.Referrer = new Uri("https://mail.example.test/?token=" + secret);
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
        var logs = await nginx.GetLogsAsync(); Assert.DoesNotContain(secret, logs.Stdout + logs.Stderr);
        Assert.Contains("\"status\":200", logs.Stdout); // Proves metadata logging is active rather than silently disabled.
    }
}
