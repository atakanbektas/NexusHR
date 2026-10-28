using DotNet.Testcontainers.Builders;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

// Opt-in only. Normal CI never downloads a model or performs inference.
public sealed class OllamaRuntimeTests
{
    private static CvAnalysisOptions Options => new()
    {
        Enabled = true, Provider = "Ollama", Model = Environment.GetEnvironmentVariable("OLLAMA_TEST_MODEL") ?? "qwen3:4b",
        TimeoutSeconds = 240, MaxOutputTokens = 4000,
        Ollama = new() { BaseUrl = Environment.GetEnvironmentVariable("OLLAMA_TEST_URL") ?? "http://127.0.0.1:11435" }
    };
    [OllamaFact]
    public async Task RealModelProducesGroundedTurkishFacts()
    {
        var options = Options; options.Validate();
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(options.Ollama.BaseUrl), Timeout = TimeSpan.FromSeconds(240) };
        var provider = new OllamaCvAnalysisProvider(http, options);
        var pages = new CvTextPage[] { new(1, """
            Tamamen sentetik Türkçe test özgeçmişi, gerçek kişi değildir.
            Profesyonel özet: Kurumsal uygulamalar geliştiren yazılım geliştirici.
            Teknik beceriler: C#, .NET, ASP.NET Core, SQL Server, PostgreSQL, Docker, React, TypeScript, RabbitMQ, Clean Architecture.
            İş deneyimi: 2022-2025 Sentetik Yazılım A.Ş., yazılım geliştirici.
            Eğitim: 2018-2022 Örnek Teknik Üniversitesi, Bilgisayar Mühendisliği.
            Sertifika: 2024 Sentetik Docker Temelleri Sertifikası.
            Yabancı diller: İngilizce B2, Almanca A2.
            """) };
        Assert.Null(await provider.GetUnavailableCodeAsync(options.Model, default));
        var result = await provider.AnalyzeAsync(pages, options.Model, default);
        EvidenceValidator.Validate(result.Result, pages);
        Assert.Contains(result.Result.Facts, x => x.Category == "skill" && x.Value.Contains("C#"));
        Assert.StartsWith("ollama:", result.RequestId); Assert.False(provider.RequiresExternalProcessing); Assert.Equal(0, options.Reservation);
        var output = Environment.GetEnvironmentVariable("OLLAMA_RESULT_PATH");
        if (!string.IsNullOrWhiteSpace(output)) await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [OllamaFact]
    public async Task ActualContainerWithoutModelAndStoppedContainerAreNotReady()
    {
        await using var server = new ContainerBuilder("ollama/ollama:0.40.12@sha256:20d1bbfbabe1c97ba4b16301f08a20d8484bcf77047742a3cb71c613d8e12d0b")
            .WithEnvironment("OLLAMA_NO_CLOUD", "1").WithPortBinding(11434, true)
            .WithCreateParameterModifier(parameters =>
            {
                foreach (var binding in parameters.HostConfig.PortBindings["11434/tcp"])
                    binding.HostIP = "127.0.0.1";
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(11434)).Build();
        await server.StartAsync();
        var options = Options; options.Ollama.BaseUrl = $"http://{server.Hostname}:{server.GetMappedPublicPort(11434)}";
        using var http = new HttpClient { BaseAddress = new Uri(options.Ollama.BaseUrl) };
        var provider = new OllamaCvAnalysisProvider(http, options);
        Assert.Equal("AI.ModelNotReady", await provider.GetUnavailableCodeAsync(options.Model, default));
        await server.StopAsync();
        Assert.Equal("AI.OllamaUnavailable", await provider.GetUnavailableCodeAsync(options.Model, default));
    }

    [OllamaFact]
    public async Task NativeInferenceHonorsCancellationAfterPreflight()
    {
        var options = Options; using var handler = new DispatchProbe();
        using var http = new HttpClient(handler) { BaseAddress = new Uri(options.Ollama.BaseUrl), Timeout = TimeSpan.FromSeconds(240) };
        var provider = new OllamaCvAnalysisProvider(http, options); using var cancel = new CancellationTokenSource();
        var task = provider.AnalyzeAsync([new(1, "Sentetik CV. Teknik beceriler: C#, .NET, PostgreSQL, Docker, TypeScript, RabbitMQ.")], options.Model, cancel.Token);
        await handler.Dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10)); await cancel.CancelAsync();
        Assert.Equal("AI.Timeout", (await Assert.ThrowsAsync<CvProviderException>(() => task)).Code);
    }
    private sealed class DispatchProbe() : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        public readonly TaskCompletionSource Dispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/chat") Dispatched.TrySetResult();
            return base.SendAsync(request, ct);
        }
    }
}
public sealed class OllamaFactAttribute : FactAttribute
{
    public OllamaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_OLLAMA_TESTS") != "1") Skip = "Opt-in real Docker Ollama tests: RUN_OLLAMA_TESTS=1 and OLLAMA_TEST_URL required; no inference in normal CI.";
    }
}
