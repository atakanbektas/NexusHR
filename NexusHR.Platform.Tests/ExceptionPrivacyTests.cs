using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using NexusHR.BuildingBlocks.Hosting;

namespace NexusHR.Platform.Tests;

public sealed class ExceptionPrivacyTests
{
    [Fact]
    public async Task UnexpectedExceptionsDoNotExposeTheirMessagesInHttpOrApplicationLogs()
    {
        const string secret = "synthetic-sensitive-provider-payload";
        var logs = new Capture();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer(); builder.AddApiDefaults(); builder.Logging.AddProvider(logs);
        await using var app = builder.Build(); app.UseApiDefaults();
        app.MapGet("/failure", (Func<IResult>)(() => throw new Exception(secret)));
        await app.StartAsync(); using var client = app.GetTestClient(); using var response = await client.GetAsync("/failure");
        Assert.Equal(500, (int)response.StatusCode); Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(secret, StringComparison.Ordinal));
        Assert.Contains(logs.Entries, entry => entry.Contains("System.Exception", StringComparison.Ordinal));
    }
    private sealed class Capture : ILoggerProvider, ILogger
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => Entries.Add(formatter(state, error) + error);
    }
}
