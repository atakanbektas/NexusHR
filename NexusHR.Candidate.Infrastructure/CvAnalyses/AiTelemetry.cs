using System.Diagnostics.Metrics;
using NexusHR.Candidate.Application.CvAnalyses;
namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

internal static class AiTelemetry
{
    internal static readonly Meter Meter = new("NexusHR.CvAnalysis", "1.0.0");
    private static readonly Counter<long> Attempts = Meter.CreateCounter<long>("nexushr.ai.attempts");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("nexushr.ai.attempt.duration", "s");
    private static readonly Counter<long> Tokens = Meter.CreateCounter<long>("nexushr.ai.tokens");
    private static readonly Counter<long> Evidence = Meter.CreateCounter<long>("nexushr.ai.evidence");
    internal static void Record(string provider, string outcome, double seconds, CvProviderResponse? response)
    {
        var tags = new System.Diagnostics.TagList { { "provider", provider == "Ollama" ? "Ollama" : "OpenAI" }, { "outcome", outcome } };
        Attempts.Add(1, tags); Duration.Record(Math.Max(0, seconds), tags);
        if (response is null) return;
        Tokens.Add(response.InputTokens, new KeyValuePair<string, object?>("direction", "input"));
        Tokens.Add(response.OutputTokens, new KeyValuePair<string, object?>("direction", "output"));
        if (response.EvidenceDiagnostics is { } d) {
            Evidence.Add(d.Proposed, new KeyValuePair<string,object?>("disposition","proposed"));
            Evidence.Add(d.Accepted, new KeyValuePair<string,object?>("disposition","accepted"));
            Evidence.Add(d.ShapeRejected+d.PageRejected+d.QuoteMissing+d.QuoteAmbiguous+d.ValueMissing, new KeyValuePair<string,object?>("disposition","rejected"));
        }
    }
}
