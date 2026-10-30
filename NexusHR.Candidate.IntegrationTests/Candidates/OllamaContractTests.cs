using System.Net;
using System.Text.Json;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class OllamaContractTests
{
    private static CvAnalysisOptions Options => new() { Enabled = true, Provider = "Ollama", Model = "qwen3:4b" };
    [Fact]
    public async Task NativeContractGroundsLiteralQuotesAndNeverSendsAnOpenAiRequest()
    {
        var handler = new Handler(); var adapter = new OllamaCvAnalysisProvider(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") }, Options);
        var result = await adapter.AnalyzeAsync([new(1, "Yazılım geliştirici. C# ve .NET.")], "qwen3:4b", default);
        Assert.Equal(21, Assert.Single(result.Result.Facts).Start);
        Assert.Equal("ollama:sha256:test-model", result.RequestId);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean()); Assert.False(body.RootElement.GetProperty("think").GetBoolean());
        Assert.Equal("object", body.RootElement.GetProperty("format").GetProperty("type").GetString());
        Assert.All(handler.Urls, url => Assert.Equal("ollama", new Uri(url).Host));
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
        var input = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains("PAGE 1\nYazılım geliştirici. C# ve .NET.", input);
    }
    [Fact]
    public async Task BlankMinimizedPageCannotBecomeAPageHeaderOnlyNativeRequest()
    {
        var handler = new Handler(); var provider = new OllamaCvAnalysisProvider(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") }, Options);
        var error = await Assert.ThrowsAsync<CvProviderException>(() => provider.AnalyzeAsync([new(1, new string(' ', 57))], "qwen3:4b", default));
        Assert.Equal("AI.NoAnalyzableText", error.Code); Assert.Empty(handler.Urls);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"facts\":[]}")]
    [InlineData("{\"facts\":null,\"warnings\":[]}")]
    public async Task RejectsMalformedMissingAndUnsupportedEvidence(string content)
    {
        var handler = new Handler { Content = content }; var provider = new OllamaCvAnalysisProvider(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") }, Options);
        var error = await Assert.ThrowsAsync<CvProviderException>(() => provider.AnalyzeAsync([new(1, "C#")], "qwen3:4b", default));
        Assert.Equal("AI.InvalidResponse", error.Code);
    }
    [Theory]
    [InlineData("{\"facts\":[{\"category\":\"skill\",\"value\":\"Java\",\"page\":1,\"quote\":\"Invented\"}],\"warnings\":[]}")]
    [InlineData("{\"facts\":[{\"category\":\"skill\",\"value\":\"C#\",\"page\":2,\"quote\":\"C#\"}],\"warnings\":[]}")]
    public async Task UnsupportedEvidenceIsDiscardedWithoutFabricatingFacts(string content)
    {
        var handler = new Handler { Content = content };
        var provider = new OllamaCvAnalysisProvider(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") }, Options);
        var response = await provider.AnalyzeAsync([new(1, "C#")], "qwen3:4b", default);
        Assert.Empty(response.Result.Facts); Assert.Equal(0, response.EvidenceDiagnostics!.Accepted);
        Assert.Single(response.Result.Warnings); // Worker records NoFindings, never useful success.
    }
    [Fact]
    public async Task MissingAndCloudModelsAreNotReady()
    {
        foreach (var handler in new[] { new Handler { Missing = true }, new Handler { Remote = true } })
        {
            var provider = new OllamaCvAnalysisProvider(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") }, Options);
            Assert.Equal(handler.Missing ? "AI.ModelNotReady" : "AI.LocalModelRequired", await provider.GetUnavailableCodeAsync("qwen3:4b", default));
            Assert.Null(handler.Body);
        }
    }
    [Fact]
    public async Task TimeoutAndConnectionFailuresAreControlled()
    {
        foreach (var failure in new Exception[] { new OperationCanceledException(), new HttpRequestException() })
        {
            var handler = new Handler { Failure = failure };
            var provider = new OllamaCvAnalysisProvider(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") }, Options);
            var error = await Assert.ThrowsAsync<CvProviderException>(() => provider.AnalyzeAsync([new(1, "C#")], "qwen3:4b", default));
            Assert.Equal(failure is OperationCanceledException ? "AI.Timeout" : "AI.OllamaUnavailable", error.Code);
        }
    }
    [Theory]
    [InlineData("https://api.openai.com/", "qwen3:4b")]
    [InlineData("http://ollama:11434/forward", "qwen3:4b")]
    [InlineData("http://user:password@ollama:11434", "qwen3:4b")]
    [InlineData("http://ollama:11434", "qwen3:cloud")]
    [InlineData("http://ollama:11434", "model-without-tag")]
    public void RefusesRemoteEndpointsAndCloudModels(string url, string model)
    {
        var options = Options; options.Ollama.BaseUrl = url; options.Model = model;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
    [Fact]
    public void LocalModeNeedsNoApiKeyOrPricesButKeepsBoundedResources()
    {
        var options = Options; options.Validate(); Assert.Equal(0, options.Reservation); Assert.Equal(0, options.EffectiveInputRate);
        options.TimeoutSeconds = 240; options.Validate();
        options.MaxParallelCalls = 100; Assert.Throws<InvalidOperationException>(options.Validate);
        options = Options; options.Provider = "OpenAI"; Assert.Throws<InvalidOperationException>(options.Validate);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string? Body; public List<string> Urls = []; public bool Missing, Remote; public Exception? Failure;
        public string Content = "{\"facts\":[{\"category\":\"skill\",\"value\":\"C#\",\"page\":1,\"quote\":\"C#\"}],\"warnings\":[\"Eğitim bilgisi yok.\"]}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (request.RequestUri.AbsolutePath == "/api/tags") return Json(Missing ? new { models = Array.Empty<object>() } :
                new { models = new object[] { new { name = "qwen3:4b", digest = "sha256:test-model" } } });
            if (request.RequestUri.AbsolutePath == "/api/show") return Json(Remote ? new { remote_host = "https://example.invalid" } : new object());
            Body = await request.Content!.ReadAsStringAsync(ct);
            if (Failure is not null) throw Failure;
            return Json(new { done = true, done_reason = "stop", message = new { content = Content }, prompt_eval_count = 100, eval_count = 50 });
        }
        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };
    }
}
