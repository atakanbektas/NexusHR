using System.Text;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

internal enum EvidenceRejection { None, Shape, Page, QuoteMissing, QuoteAmbiguous, ValueMissing }
internal sealed record EvidenceResolution(CvFact? Fact, EvidenceRejection Rejection, bool WhitespaceResolved = false);

internal static class OllamaEvidenceResolver
{
    internal static EvidenceResolution Resolve(string category, string value, int number, string quote, IReadOnlyList<CvTextPage> pages)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 500 || string.IsNullOrWhiteSpace(quote) || quote.Length > 1500 ||
            category is not ("summary" or "skill" or "employment" or "education" or "language" or "certification"))
            return new(null, EvidenceRejection.Shape);
        var page = pages.SingleOrDefault(p => p.Number == number);
        if (page is null) return new(null, EvidenceRejection.Page);
        var match = Find(page.Text, quote);
        if (match.Ambiguous) return new(null, EvidenceRejection.QuoteAmbiguous);
        if (match.Start < 0) return new(null, EvidenceRejection.QuoteMissing);
        var originalQuote = page.Text.Substring(match.Start, match.Length);
        if (originalQuote.Length > 1500) return new(null, EvidenceRejection.Shape);
        // Never search outside the proposed evidence or rewrite spelling/punctuation/meaning.
        var originalValue = value;
        var normalized = match.Normalized;
        if (!originalQuote.Contains(value, StringComparison.Ordinal))
        {
            var valueMatch = Find(originalQuote, value);
            if (valueMatch.Start < 0 || valueMatch.Ambiguous) return new(null, EvidenceRejection.ValueMissing);
            originalValue = originalQuote.Substring(valueMatch.Start, valueMatch.Length);
            normalized |= valueMatch.Normalized;
        }
        var fact = new CvFact(category, originalValue, number, match.Start, originalQuote);
        try { EvidenceValidator.Validate(new([fact], []), pages); }
        catch (InvalidDataException) { return new(null, EvidenceRejection.Shape); }
        return new(fact, EvidenceRejection.None, normalized);
    }

    private readonly record struct Match(int Start, int Length, bool Ambiguous = false, bool Normalized = false);
    private static Match Find(string source, string needle)
    {
        var at = source.IndexOf(needle, StringComparison.Ordinal);
        if (at >= 0) return source.IndexOf(needle, at + 1, StringComparison.Ordinal) >= 0 ? new(-1, 0, true) : new(at, needle.Length);
        var normalizedSource = Canonicalize(source);
        var normalizedNeedle = Canonicalize(needle).Text;
        if (normalizedNeedle.Length == 0) return new(-1, 0);
        at = normalizedSource.Text.IndexOf(normalizedNeedle, StringComparison.Ordinal);
        if (at < 0) return new(-1, 0);
        if (normalizedSource.Text.IndexOf(normalizedNeedle, at + 1, StringComparison.Ordinal) >= 0) return new(-1, 0, true);
        var start = normalizedSource.Starts[at];
        var end = normalizedSource.Ends[at + normalizedNeedle.Length - 1];
        return new(start, end - start, Normalized: true);
    }
    private sealed record Canonical(string Text, List<int> Starts, List<int> Ends);
    private static Canonical Canonicalize(string text)
    {
        var output = new StringBuilder(); var starts = new List<int>(); var ends = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            var start = i;
            if (char.IsWhiteSpace(text[i]))
            {
                while (i + 1 < text.Length && char.IsWhiteSpace(text[i + 1])) i++;
                if (output.Length == 0 || i == text.Length - 1) continue;
                output.Append(' ');
            }
            else output.Append(text[i]);
            starts.Add(start); ends.Add(i + 1);
        }
        return new(output.ToString(), starts, ends);
    }
}
