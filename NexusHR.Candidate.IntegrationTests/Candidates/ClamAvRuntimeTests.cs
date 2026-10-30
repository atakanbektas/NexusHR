using DotNet.Testcontainers.Builders;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class ClamAvRuntimeTests
{
    [ClamAvFact]
    public async Task RealDaemonAcceptsCleanPdfAndRejectsHarmlessEicarTestInMemory()
    {
        await using var server = new ContainerBuilder("clamav/clamav:1.5.4@sha256:bda39705e291105049c3936e2803ac90b955172af8030f94f05dce1252df6c6d")
            .WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true").WithPortBinding(3310, true)
            .WithCreateParameterModifier(parameters => {
                var ports = parameters.HostConfig?.PortBindings;
                if (ports is null || !ports.TryGetValue("3310/tcp", out var bindings) || bindings is null) throw new InvalidOperationException("Explicit test port binding required.");
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310)).Build();
        await server.StartAsync();
        var scanner = new ClamAvDocumentScanner(new() { Enabled = true, Host = "127.0.0.1", Port = server.GetMappedPublicPort(3310) });
        await scanner.ScanAsync(AiFixture.Pdf, default);
        // Standard harmless AV test string. Decode only in memory; never write an executable/test-virus file.
        var eicar = Convert.FromBase64String("WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo=");
        var detected = await Assert.ThrowsAsync<CvAnalysisException>(() => scanner.ScanAsync(eicar, default));
        Assert.Equal("Candidate.UnsafeDocument", detected.Code);
    }
}
internal sealed class ClamAvFactAttribute : FactAttribute
{
    public ClamAvFactAttribute() { if (Environment.GetEnvironmentVariable("RUN_CLAMAV_TESTS") != "1") Skip = "Opt-in real local ClamAV daemon (3-4 GiB RAM)."; }
}
