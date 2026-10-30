namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

public sealed class CvAnalysisOptions
{
    public bool Enabled { get; set; }
    public bool AllowExternalProcessing { get; set; }
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string PriceVersion { get; set; } = "";
    public decimal InputUsdPerMillion { get; set; }
    public decimal OutputUsdPerMillion { get; set; }
    public decimal DailyBudgetUsd { get; set; } = 5;
    public int RequestsPerUserPerDay { get; set; } = 20;
    public int MaxInputCharacters { get; set; } = 12000;
    public int MaxOutputTokens { get; set; } = 2000;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 2;
    // JSON escaping can use six ASCII bytes per UTF-16 code unit; reserve that upper
    // bound plus prompt/schema overhead rather than estimating tokens from words.
    public decimal Reservation => decimal.Ceiling(((MaxInputCharacters * 6L + 8192) * InputUsdPerMillion + MaxOutputTokens * OutputUsdPerMillion) * 100) / 100_000_000m;
    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Model) || Model.Length > 200 || string.IsNullOrWhiteSpace(PriceVersion) || PriceVersion.Length > 100 || InputUsdPerMillion <= 0 ||
            OutputUsdPerMillion <= 0 || DailyBudgetUsd <= 0 || RequestsPerUserPerDay < 1 || MaxInputCharacters is < 100 or > 40000 ||
            MaxOutputTokens is < 100 or > 8000 || TimeoutSeconds is < 1 or > 120 || MaxAttempts is < 1 or > 3)
            throw new InvalidOperationException("AI analysis requires explicit model, versioned positive prices and bounded limits.");
    }
}
