using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Documents;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class LocalOcrRuntimeTests : IAsyncLifetime
{
    private IContainer? container;
    private HttpClient? http;
    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("RUN_LOCAL_OCR_TESTS") != "1") return;
        container = new ContainerBuilder("nexushr-local-ocr:1").WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x => x.ForPort(8080).ForPath("/health"))).Build();
        await container.StartAsync();
        http = new HttpClient { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(8080)}"), Timeout = TimeSpan.FromSeconds(95) };
    }
    [LocalOcrFact]
    public async Task ActualTesseractHandlesTurkishEnglishAndSupportedImageAndScanFormats()
    {
        foreach (var format in new[] { "png", "jpg", "webp", "scan", "multi" })
        {
            var bytes = await FixtureAsync(format);
            var pages = await new LocalCvTextExtractor(http!, new() { Enabled = true }, new ClamAvDocumentScanner(new())).ExtractPagesAsync(bytes, default);
            Assert.Equal(format == "multi" ? 2 : 1, pages.Count);
            Assert.All(pages, page => {
                Assert.Contains("PostgreSQL", page.Text); Assert.Contains("Engineer", page.Text);
                Assert.Contains("Geliştirici", page.Text); Assert.NotEmpty(page.Words);
                Assert.StartsWith("tesseract", page.Source);
            });
        }
    }
    [LocalOcrFact]
    public async Task RealDecoderRejectsCorruptionAndNativeOcrReportsBlankAndLowQualityInput()
    {
        using var invalid = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0]);
        Assert.False((await http!.PostAsync("validate", invalid)).IsSuccessStatusCode);
        var blankBytes = await FixtureAsync("blank");
        var blank = await Assert.ThrowsAsync<CvProviderException>(() => new LocalCvTextExtractor(http!, new() { Enabled = true }, new ClamAvDocumentScanner(new()))
            .ExtractPagesAsync(blankBytes, default));
        Assert.Equal("AI.OcrNoReadableText", blank.Code);
        var low = await FixtureAsync("low");
        try
        {
            var pages = await new LocalCvTextExtractor(http!, new() { Enabled = true }, new ClamAvDocumentScanner(new())).ExtractPagesAsync(low, default);
            // Native confidence/provenance is retained even if low-quality glyphs remain readable.
            Assert.All(pages, p => Assert.StartsWith("tesseract", p.Source));
        }
        catch (CvProviderException ex) { Assert.Equal("AI.OcrNoReadableText", ex.Code); }
    }
    private async Task<byte[]> FixtureAsync(string kind)
    {
        var result = await container!.ExecAsync(["python3", "/app/fixtures.py", kind]);
        Assert.Equal(0, result.ExitCode); return Convert.FromBase64String(result.Stdout.Trim());
    }
    [LocalOcrFact]
    public async Task CancelledOcrRequestDoesNotLeaveNativeWorkerRunning()
    {
        var bytes = await FixtureAsync("multi"); using var cancel = new CancellationTokenSource();
        var script = """
            import os, pathlib
            count=0
            for path in pathlib.Path('/proc').glob('[0-9]*/cmdline'):
                try:
                    if path.parent.name != str(os.getpid()) and b'/app/worker.py' in path.read_bytes(): count+=1
                except OSError: pass
            print(count)
            """;
        using var content = new ByteArrayContent(bytes);
        var request = http!.PostAsync("extract", content, cancel.Token);
        // Observe actual native work before cancelling; cancelling before dispatch would prove nothing.
        var startedBy = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var running = await container!.ExecAsync(["python3", "-c", script]); Assert.Equal(0, running.ExitCode);
            if (int.Parse(running.Stdout.Trim()) > 0) break;
            Assert.True(!request.IsCompleted && DateTime.UtcNow < startedBy, "Native OCR worker was not observed before cancellation.");
            await Task.Delay(50);
        }
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await request; });
        var until = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var process = await container!.ExecAsync(["python3", "-c", script]); Assert.Equal(0, process.ExitCode);
            if (process.Stdout.Trim() == "0") break;
            Assert.True(DateTime.UtcNow < until, "Cancelled document left an OCR worker running."); await Task.Delay(100);
        }
    }
    public async Task DisposeAsync() { http?.Dispose(); if (container is not null) await container.DisposeAsync(); }
}
internal sealed class LocalOcrFactAttribute : FactAttribute
{
    public LocalOcrFactAttribute() { if (Environment.GetEnvironmentVariable("RUN_LOCAL_OCR_TESTS") != "1") Skip = "Opt-in actual local OCR Docker tests."; }
}
