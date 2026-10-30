using System.Globalization;

namespace NexusHR.Candidate.Application.CvAnalyses;

public static class ExperienceCoverage
{
    // Calendar-month coverage, not inferred working time. Ambiguous/missing/open dates stay absent.
    public static int? CalendarMonths(IEnumerable<CvFact> facts)
    {
        var months = new HashSet<int>();
        foreach (var fact in facts.Where(f => f.Category == "employment"))
        {
            var start = fact.Fields?.FirstOrDefault(f => f.Name == "start")?.Value;
            var end = fact.Fields?.FirstOrDefault(f => f.Name == "end")?.Value;
            if (!TryMonth(start, out var a) || !TryMonth(end, out var b) || b < a || b - a > 1200) continue;
            for (var month = a; month <= b; month++) months.Add(month);
        }
        return months.Count == 0 ? null : months.Count;
    }
    private static bool TryMonth(string? text, out int month)
    {
        month = 0;
        if (!DateOnly.TryParseExact(text, ["yyyy-MM", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year is < 1900 or > 2200)
            return false;
        month = date.Year * 12 + date.Month - 1; return true;
    }
}
