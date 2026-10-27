using System.Net;
using System.Text.Json;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class OpenAiContractTests
{
    private static CvAnalysisOptions Options => new() { Enabled = true, AllowExternalProcessing = true, ApiKey = "test-only-not-a-key", Model = "test-model" };
    [Fact]
    public async Task WireContractUsesResponsesStrictSchemaNoStorageAndNoTools()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"id":"resp_test","status":"completed","output":[{"content":[{"type":"output_text","text":"{\"facts\":[{\"category\":\"skill\",\"value\":\"C#\",\"page\":1,\"start\":0,\"quote\":\"C#\"}],\"warnings\":[]}"}]}],"usage":{"input_tokens":50,"output_tokens":20}}
            """);
        var adapter = new OpenAiCvAnalysisProvider(new HttpClient(handler), Options);
        var response = await adapter.AnalyzeAsync([new(1, "C#")], "test-model", default);
        Assert.Equal("resp_test", response.RequestId);
        Assert.Equal("C#", Assert.Single(response.Result.Facts).Value);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.True(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
        Assert.Equal("https://api.openai.com/v1/responses", handler.Url);
        Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData(429, "{\"error\":{\"code\":\"rate_limit_exceeded\"}}", "AI.RateLimited", true)]
    [InlineData(429, "{\"error\":{\"code\":\"insufficient_quota\"}}", "AI.ProviderQuotaExceeded", false)]
    [InlineData(401, "{}", "AI.ProviderRejected", false)]
    [InlineData(500, "{}", "AI.OutcomeUnknown", false)]
    [InlineData(200, "{\"status\":\"incomplete\"}", "AI.IncompleteResponse", false)]
    [InlineData(200, "{\"status\":\"completed\",\"output\":[{\"content\":[{\"type\":\"refusal\"}]}]}", "AI.Refused", false)]
    [InlineData(200, "{}", "AI.InvalidResponse", false)]
    public async Task ClassifiesFailuresWithoutAutomaticHttpRetries(int code, string body, string expected, bool retry)
    {
        var handler = new Handler((HttpStatusCode)code, body);
        var error = await Assert.ThrowsAsync<CvProviderException>(() => new OpenAiCvAnalysisProvider(new HttpClient(handler), Options).AnalyzeAsync([new(1, "C#")], "test-model", default));
        Assert.Equal(expected, error.Code); Assert.Equal(retry, error.SafeToRetry); Assert.Equal(1, handler.Calls);
        if (retry) Assert.Equal(TimeSpan.FromSeconds(17), error.RetryAfter);
    }
    [Fact]
    public async Task ExternalProcessingRequiresSeparateExplicitSwitch()
    {
        var handler = new Handler(HttpStatusCode.OK, "{}"); var options = Options; options.AllowExternalProcessing = false;
        await Assert.ThrowsAsync<CvProviderException>(() => new OpenAiCvAnalysisProvider(new HttpClient(handler), options).AnalyzeAsync([], "test-model", default));
        Assert.Equal(0, handler.Calls);
    }
    private sealed class Handler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Body, Url; public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(ct); Url = request.RequestUri!.ToString();
            var result = new HttpResponseMessage(status) { Content = new StringContent(response) };
            result.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(17)); return result;
        }
    }
}
