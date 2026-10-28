using System.Text.RegularExpressions;
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
            m => new string(' ', m.Length), RegexOptions.None, TimeSpan.FromSeconds(1));
    }
}
