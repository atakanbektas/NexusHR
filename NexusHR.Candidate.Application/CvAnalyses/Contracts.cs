using NexusHR.Candidate.Domain.CvAnalyses;

namespace NexusHR.Candidate.Application.CvAnalyses;

public sealed record CvTextPage(int Number, string Text)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int ImageCount { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public string Source { get; init; } = "pdfpig";
    [System.Text.Json.Serialization.JsonIgnore] public IReadOnlyList<CvWordBox> Words { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public bool RequiresOcr { get; init; }
}
public sealed record CvWordBox(int Start, int Length, int X, int Y, int Width, int Height, double Confidence);
public sealed record CvEvidenceRegion(int X, int Y, int Width, int Height);
public sealed class CvTextUnavailableException(int pages, int imagePages, int characters) : Exception(
    "PDF’den okunabilir metin çıkarılamadı. Görsel/tarama tabanlı belge için bu ortamda yerel OCR etkinleştirilmelidir.")
{
    public int PageCount { get; } = pages;
    public int ImagePageCount { get; } = imagePages;
    public int CharacterCount { get; } = characters;
}
public interface ICvTextExtractor
{
    Task<IReadOnlyList<CvTextPage>> ExtractPagesAsync(byte[] pdf, CancellationToken ct);
}
public sealed record CvField(string Name, string Value, string Quote);
public sealed record CvFact(string Category, string Value, int Page, int Start, string Quote)
{
    public IReadOnlyList<CvField>? Fields { get; init; }
    public string Source { get; init; } = "pdfpig";
    public IReadOnlyList<CvEvidenceRegion> Regions { get; init; } = [];
}
public sealed record CvAnalysisResult(IReadOnlyList<CvFact> Facts, IReadOnlyList<string> Warnings)
{
    public int? ExperienceCalendarMonths => ExperienceCoverage.CalendarMonths(Facts);
}
public sealed record CvProviderResponse(CvAnalysisResult Result, string RequestId, int InputTokens, int OutputTokens)
{
    public CvEvidenceDiagnostics? EvidenceDiagnostics { get; init; }
}
// Counts only. Never include model text, rejected quotes, prompts or identity in diagnostics.
public sealed record CvEvidenceDiagnostics(int Proposed, int Accepted, int ShapeRejected, int PageRejected,
    int QuoteMissing, int QuoteAmbiguous, int ValueMissing, int WhitespaceResolved, int Duplicates);


public interface ICvAnalysisProvider
{
    bool RequiresExternalProcessing => true;
    Task<string?> GetUnavailableCodeAsync(string model, CancellationToken ct) => Task.FromResult<string?>(null);
    Task<CvProviderResponse> AnalyzeAsync(IReadOnlyList<CvTextPage> pages, string model, CancellationToken ct);
}
public sealed class CvProviderException(string code, bool safeToRetry = false, TimeSpan? retryAfter = null, string? stage = null) : Exception(code)
{
    public string Code { get; } = code;
    public bool SafeToRetry { get; } = safeToRetry;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public string? Stage { get; } = stage;
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
    IReadOnlyList<ReviewView> Reviews, IReadOnlyList<AttemptView> Attempts, string Provider = "OpenAI", string Stage = "");
public sealed record ReviewView(string Reviewer, string Decision, string Notes, DateTime CreatedAtUtc, int Revision,
    int? FactIndex = null, string? CorrectedValue = null);
public sealed record AttemptView(int Number, string Outcome, int? InputTokens, int? OutputTokens, decimal ReservedUsd, decimal? UsageCostUsd, string PriceVersion);

public interface ICvAnalysisStore
{
    Task<AnalysisView> RequestAsync(Guid candidateId, Guid documentId, string user, string key, bool consent, CancellationToken ct);
    Task<AnalysisHistory> ListAsync(Guid candidateId, int page, CancellationToken ct);
    Task<AnalysisView> GetAsync(Guid candidateId, Guid id, CancellationToken ct);
    Task<AnalysisView> MutateAsync(Guid candidateId, Guid id, string user, uint version, string action, bool acknowledgeUnknown, string? decision, string? notes, CancellationToken ct,
        int? factIndex = null, string? correctedValue = null);
}

public static class EvidenceValidator
{
    public static void Validate(CvAnalysisResult result, IReadOnlyList<CvTextPage> pages)
    {
        if (result.Facts is null || result.Warnings is null || result.Facts.Count > 100 || result.Warnings.Count > 20 ||
            result.Warnings.Any(x => x is null || x.Length > 500)) throw new InvalidDataException("Invalid analysis shape.");
        foreach (var fact in result.Facts)
        {
            if (fact is null || fact.Category is not ("summary" or "skill" or "employment" or "education" or "language" or "certification" or "project" or "technology" or "specialization" or "experience") ||
                string.IsNullOrWhiteSpace(fact.Value) || fact.Value.Length > 500 || string.IsNullOrWhiteSpace(fact.Quote) || fact.Quote.Length > 1500)
                throw new InvalidDataException("Invalid fact.");
            var page = pages.SingleOrDefault(x => x.Number == fact.Page);
            if (page is null || fact.Start < 0 || (long)fact.Start + fact.Quote.Length > page.Text.Length ||
                !page.Text.AsSpan(fact.Start, fact.Quote.Length).SequenceEqual(fact.Quote.AsSpan()) ||
                !fact.Quote.Contains(fact.Value, StringComparison.Ordinal)) throw new InvalidDataException("Ungrounded fact.");
            if (fact.Fields?.Count > 12) throw new InvalidDataException("Too many fields.");
            foreach (var field in fact.Fields ?? [])
                if (field is null || field.Name is not ("company" or "position" or "start" or "end" or "duration" or "institution" or "department" or "degree" or "date" or "category" or "level") ||
                    string.IsNullOrWhiteSpace(field.Value) || field.Value.Length > 200 || string.IsNullOrWhiteSpace(field.Quote) || field.Quote.Length > 1500 ||
                    !field.Quote.Contains(field.Value, StringComparison.Ordinal) || !fact.Quote.Contains(field.Quote, StringComparison.Ordinal) ||
                    fact.Quote.IndexOf(field.Quote, StringComparison.Ordinal) != fact.Quote.LastIndexOf(field.Quote, StringComparison.Ordinal))
                    throw new InvalidDataException("Ungrounded structured field.");
            if ((fact.Fields ?? []).GroupBy(f => f.Name).Any(g => g.Count() > 1)) throw new InvalidDataException("Duplicate field.");
        }
    }
}
