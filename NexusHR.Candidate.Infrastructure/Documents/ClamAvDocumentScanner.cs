using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Infrastructure.Documents;

public sealed class DocumentSafetyOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "cv-antivirus";
    public int Port { get; set; } = 3310;
    public void Validate()
    {
        if (Enabled && (Port is < 1 or > 65535 || (Host != "cv-antivirus" && Host != "localhost" && Host != "127.0.0.1")))
            throw new InvalidOperationException("Use a private Docker/loopback ClamAV endpoint.");
    }
}

internal sealed class ClamAvDocumentScanner(DocumentSafetyOptions options) : ICvDocumentScanner
{
    public async Task ScanAsync(byte[] content, CancellationToken ct)
    {
        if (!options.Enabled) return; // Explicit development opt-out; Production host requires scanning.
        if (content.Length is 0 or > 5 * 1024 * 1024) throw new CvAnalysisException(400, "Candidate.CvValidationFailed");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(options.Host, options.Port, deadline.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), deadline.Token);
            var header = new byte[4];
            for (var offset = 0; offset < content.Length; offset += 32768)
            {
                var size = Math.Min(32768, content.Length - offset); BinaryPrimitives.WriteInt32BigEndian(header, size);
                await stream.WriteAsync(header, deadline.Token); await stream.WriteAsync(content.AsMemory(offset, size), deadline.Token);
            }
            await stream.WriteAsync(new byte[4], deadline.Token);
            var response = new byte[512]; var length = 0;
            while (length < response.Length)
            {
                var count = await stream.ReadAsync(response.AsMemory(length, 1), deadline.Token);
                if (count == 0) break;
                if (response[length++] == 0)
                {
                    var result = Encoding.UTF8.GetString(response, 0, length - 1);
                    if (result == "stream: OK") return;
                    if (result.EndsWith(" FOUND", StringComparison.Ordinal)) throw new CvAnalysisException(422, "Candidate.UnsafeDocument");
                    break;
                }
            }
            throw new CvAnalysisException(503, "Candidate.ScanUnavailable");
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            // Fail closed. Do not leak scanner response/signature/file content into logs or HTTP errors.
            throw new CvAnalysisException(503, "Candidate.ScanUnavailable");
        }
    }
}
