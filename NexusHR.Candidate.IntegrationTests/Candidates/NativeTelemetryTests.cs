using System.Text.Json;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class NativeTelemetryTests
{
    [Theory]
    [InlineData("{\"eval_count\":40,\"eval_duration\":2000000000}",20)]
    [InlineData("{\"eval_count\":0,\"eval_duration\":2000000000}",0)]
    public void NativeRateUsesProviderNanosecondsNotPipelineDuration(string json,double expected) =>
        Assert.Equal(expected,OllamaCvAnalysisProvider.NativeTokenRate(JsonSerializer.Deserialize<JsonElement>(json)));
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"eval_count\":40,\"eval_duration\":0}")]
    [InlineData("{\"eval_count\":40,\"eval_duration\":\"unavailable\"}")]
    [InlineData("{\"eval_count\":-1,\"eval_duration\":1}")]
    public void MissingOrMalformedTimingIsUnknownRatherThanZeroOrAnalysisFailure(string json) =>
        Assert.Null(OllamaCvAnalysisProvider.NativeTokenRate(JsonSerializer.Deserialize<JsonElement>(json)));
}
