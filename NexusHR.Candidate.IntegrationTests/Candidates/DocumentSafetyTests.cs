using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class DocumentSafetyTests
{
    [Theory]
    [InlineData("stream: OK\0", null)]
    [InlineData("stream: Test.Signature FOUND\0", "Candidate.UnsafeDocument")]
    [InlineData("stream: size limit exceeded ERROR\0", "Candidate.ScanUnavailable")]
    [InlineData("stream: OK", "Candidate.ScanUnavailable")]
    public async Task InStreamProtocolIsBoundedAndFailsClosed(string response, string? expected)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var content = new byte[70000]; Random.Shared.NextBytes(content);
            var server = Task.Run(async () => {
                using var client = await listener.AcceptTcpClientAsync(); await using var stream = client.GetStream();
                var command = new byte[10]; await stream.ReadExactlyAsync(command); Assert.Equal("zINSTREAM\0", Encoding.UTF8.GetString(command));
                using var received = new MemoryStream(); var header = new byte[4];
                while (true) { await stream.ReadExactlyAsync(header); var size = BinaryPrimitives.ReadInt32BigEndian(header); if (size == 0) break;
                    Assert.InRange(size, 1, 32768); var chunk = new byte[size]; await stream.ReadExactlyAsync(chunk); received.Write(chunk); }
                Assert.Equal(content, received.ToArray()); await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            });
            var scanner = new ClamAvDocumentScanner(new() { Enabled = true, Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port });
            if (expected is null) await scanner.ScanAsync(content, default);
            else Assert.Equal(expected, (await Assert.ThrowsAsync<CvAnalysisException>(() => scanner.ScanAsync(content, default))).Code);
            await server;
        }
        finally { listener.Stop(); }
    }
    [Fact]
    public async Task UnavailableScannerDoesNotAcceptDocuments()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var scanner = new ClamAvDocumentScanner(new() { Enabled = true, Host = "127.0.0.1", Port = port });
        Assert.Equal("Candidate.ScanUnavailable", (await Assert.ThrowsAsync<CvAnalysisException>(() => scanner.ScanAsync(AiFixture.Pdf, default))).Code);
    }
}
