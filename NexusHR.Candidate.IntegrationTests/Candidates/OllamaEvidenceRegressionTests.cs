using System.Net;
using System.Text.Json;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class OllamaEvidenceRegressionTests
{
    private static async Task<CvProviderResponse> Analyze(string source, object[] facts)
    {
        var options = new CvAnalysisOptions { Enabled = true, Provider = "Ollama", Model = "qwen3:4b" };
        using var http = new HttpClient(new NativeReply(JsonSerializer.Serialize(new { facts, warnings = Array.Empty<string>() }))) { BaseAddress = new Uri("http://ollama:11434") };
        return await new OllamaCvAnalysisProvider(http, options).AnalyzeAsync([new(1, source)], options.Model, default);
    }
    [Fact]
    public async Task OneBadQuoteCannotDiscardValidProfessionalEvidence()
    {
        var result = await Analyze("C# PostgreSQL", [new { category = "skill", value = "C#", page = 1, quote = "C#" },
            new { category = "skill", value = "Java", page = 1, quote = "Invented Java" }]);
        Assert.Equal("C#", Assert.Single(result.Result.Facts).Value);
        Assert.Contains(result.Result.Warnings, w => w.Contains("1", StringComparison.Ordinal));
    }
    [Fact]
    public async Task WhitespaceOnlyNormalizationRestoresOriginalSourceAndOffsets()
    {
        const string source = "Beceri: C#\r\n  PostgreSQL\u00a0Docker";
        var result = await Analyze(source, [new { category = "skill", value = "C# PostgreSQL", page = 1, quote = "C# PostgreSQL Docker" }]);
        var fact = Assert.Single(result.Result.Facts);
        Assert.Equal(8, fact.Start); Assert.Equal("C#\r\n  PostgreSQL\u00a0Docker", fact.Quote); Assert.Equal("C#\r\n  PostgreSQL", fact.Value);
        EvidenceValidator.Validate(result.Result, [new(1, source)]);
    }
    [Theory]
    [InlineData("C# ... C#", "C#", "C#", 1)]
    [InlineData("C#\nPostgreSQL ... C#   PostgreSQL", "C# PostgreSQL", "C#", 1)]
    [InlineData("C# PostgreSQL", "C#", "C#", 2)]
    [InlineData("C# PostgreSQL", "C#", "Java", 1)]
    [InlineData("C# PostgreSQL", "c#", "c#", 1)]
    public async Task AmbiguousWrongPageMissingValueAndSemanticChangesAreRejected(string source, string quote, string value, int page)
    {
        var result = await Analyze(source, [new { category = "skill", value, page, quote }]);
        Assert.Empty(result.Result.Facts);
    }
    [Fact]
    public async Task InvalidFactShapeDoesNotPoisonValidSiblingAndDuplicatesAreNotRepeated()
    {
        var result = await Analyze("C# PostgreSQL", [new { category = "skill", value = "C#", page = 1, quote = "C#" },
            new { category = "skill", value = "C#", page = 1, quote = "C#" }, new { category = "skill", value = "PostgreSQL", page = "wrong", quote = "PostgreSQL" }]);
        Assert.Single(result.Result.Facts);
    }
    internal sealed class NativeReply(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(request.RequestUri!.AbsolutePath switch {
                "/api/tags" => """{"models":[{"name":"qwen3:4b","digest":"synthetic"}]}""",
                "/api/show" => "{}",
                _ => JsonSerializer.Serialize(new { done = true, done_reason = "stop", message = new { content }, prompt_eval_count = 100, eval_count = 50 })
            }) });
    }
}
