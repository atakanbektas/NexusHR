using System.Text.RegularExpressions;
using System.Globalization;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

internal sealed record CvPageMetrics(int Page, int RawCharacters, int RawReadableCharacters,
    int MinimizedCharacters, int MinimizedReadableCharacters, int Images);
internal sealed record PreparedCvInput(IReadOnlyList<CvTextPage> Pages, IReadOnlyList<CvPageMetrics> Metrics,
    int ImagePages, int ExcludedPages);

internal static class CvAnalysisInput
{
    public static bool HasReadableText(CvTextPage page) => page.Text.Any(char.IsLetterOrDigit);

    public static PreparedCvInput Prepare(IReadOnlyList<CvTextPage> pages, params string[] identifiers)
    {
        var clean = pages.Select(p => p with { Text = Redact(p.Text, identifiers) }).ToArray();
        var metrics = pages.Zip(clean, (raw, minimized) => new CvPageMetrics(raw.Number, raw.Text.Length,
            raw.Text.Count(char.IsLetterOrDigit), minimized.Text.Length, minimized.Text.Count(char.IsLetterOrDigit), raw.ImageCount)).ToArray();
        // Exclude unusable pages while preserving their original page numbers and all text offsets.
        var usable = clean.Where(HasReadableText).ToArray();
        return new(usable, metrics, pages.Count(x => x.ImageCount > 0), pages.Count - usable.Length);
    }

    private static string Redact(string text, string[] identifiers)
    {
        // Preserve the existing minimization policy and exact evidence offsets.
        foreach (var value in identifiers.Where(x => x.Length >= 2).OrderByDescending(x => x.Length))
            text = Regex.Replace(text, Regex.Escape(value), m => new string(' ', m.Length), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return Regex.Replace(text, @"[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}|(?:\+?\d[\d ()-]{8,}\d)",
            m => IsProfessionalDate(m, text) ? m.Value : new string(' ', m.Length), RegexOptions.None, TimeSpan.FromSeconds(1));
    }
    private static bool IsProfessionalDate(Match match, string text)
    {
        // A phone-like span may actually be an ISO employment/education date range.
        // Known candidate identifiers were already masked above, even if date-shaped.
        var parts = match.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2 || parts.Any(p => !DateOnly.TryParseExact(p, ["yyyy-MM", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year is < 1900 or > 2200)) return false;
        var start = match.Index == 0 ? 0 : text.LastIndexOf('\n', match.Index - 1) + 1;
        var end = text.IndexOf('\n', match.Index); if (end < 0) end = text.Length;
        var line = text[start..end];
        if (Regex.IsMatch(line, @"\b(phone|telephone|mobile|contact|tel|telefon|iletişim)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) return false;
        return Regex.IsMatch(line, @"\b(employment|experience|education|engineer|engineering|developer|university|deneyim|mühendis|geliştirici|üniversite|eğitim)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
