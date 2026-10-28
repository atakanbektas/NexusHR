using NexusHR.Candidate.Domain.CvAnalyses;

namespace NexusHR.Candidate.Application.CvAnalyses;

public sealed record CvTextPage(int Number, string Text);
public interface ICvTextExtractor
{
    Task<IReadOnlyList<CvTextPage>> ExtractPagesAsync(byte[] pdf, CancellationToken ct);
}
public sealed record CvFact(string Category, string Value, int Page, int Start, string Quote);
public sealed record CvAnalysisResult(IReadOnlyList<CvFact> Facts, IReadOnlyList<string> Warnings);
public sealed record CvProviderResponse(CvAnalysisResult Result, string RequestId, int InputTokens, int OutputTokens);
public interface ICvAnalysisProvider
{
    bool RequiresExternalProcessing => true;
    Task<string?> GetUnavailableCodeAsync(string model, CancellationToken ct) => Task.FromResult<string?>(null);
    Task<CvProviderResponse> AnalyzeAsync(IReadOnlyList<CvTextPage> pages, string model, CancellationToken ct);
}
public sealed class CvProviderException(string code, bool safeToRetry = false, TimeSpan? retryAfter = null) : Exception(code)
{
    public string Code { get; } = code;
    public bool SafeToRetry { get; } = safeToRetry;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
public sealed class CvAnalysisException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record AnalysisSettings(bool Enabled, bool ExternalProcessingAllowed, string Provider, decimal MaxReservationUsd,
    bool Ready = true, string? UnavailableCode = null);
public sealed record AnalysisHistory(AnalysisSettings Settings, Guid? DocumentId, IReadOnlyList<AnalysisView> Items, int Page, int TotalCount);
public sealed record AnalysisView(Guid Id, Guid DocumentId, string Status, uint Version, bool CancellationRequested,
    DateTime CreatedAtUtc, string? ErrorCode, string Model, string? ContentHash, CvAnalysisResult? Result,
    IReadOnlyList<ReviewView> Reviews, IReadOnlyList<AttemptView> Attempts, string Provider = "OpenAI");
public sealed record ReviewView(string Reviewer, string Decision, string Notes, DateTime CreatedAtUtc, int Revision);
public sealed record AttemptView(int Number, string Outcome, int? InputTokens, int? OutputTokens, decimal ReservedUsd, decimal? UsageCostUsd, string PriceVersion);

public interface ICvAnalysisStore
{
    Task<AnalysisView> RequestAsync(Guid candidateId, Guid documentId, string user, string key, bool consent, CancellationToken ct);
    Task<AnalysisHistory> ListAsync(Guid candidateId, int page, CancellationToken ct);
    Task<AnalysisView> GetAsync(Guid candidateId, Guid id, CancellationToken ct);
    Task<AnalysisView> MutateAsync(Guid candidateId, Guid id, string user, uint version, string action, bool acknowledgeUnknown, string? decision, string? notes, CancellationToken ct);
}

public static class EvidenceValidator
{
    public static void Validate(CvAnalysisResult result, IReadOnlyList<CvTextPage> pages)
    {
        if (result.Facts is null || result.Warnings is null || result.Facts.Count > 100 || result.Warnings.Count > 20 ||
            result.Warnings.Any(x => x is null || x.Length > 500)) throw new InvalidDataException("Invalid analysis shape.");
        foreach (var fact in result.Facts)
        {
            if (fact is null || fact.Category is not ("summary" or "skill" or "employment" or "education" or "language" or "certification") ||
                string.IsNullOrWhiteSpace(fact.Value) || fact.Value.Length > 500 || string.IsNullOrWhiteSpace(fact.Quote) || fact.Quote.Length > 1500)
                throw new InvalidDataException("Invalid fact.");
            var page = pages.SingleOrDefault(x => x.Number == fact.Page);
            if (page is null || fact.Start < 0 || (long)fact.Start + fact.Quote.Length > page.Text.Length ||
                !page.Text.AsSpan(fact.Start, fact.Quote.Length).SequenceEqual(fact.Quote.AsSpan()) ||
                !fact.Quote.Contains(fact.Value, StringComparison.Ordinal)) throw new InvalidDataException("Ungrounded fact.");
        }
    }
}
