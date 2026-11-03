using System.Text.Json;

namespace NexusHR.Gateway.Engineering;

public sealed class EngineeringOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionEnabled { get; set; }
    public bool InitializeDatabase { get; set; }
    public int MaxVus { get; set; } = 100;
    public string AgentKey { get; set; } = "";
    public string Database { get; set; } = "";
    public string Prometheus { get; set; } = "http://engineering-prometheus:9090";
    public string Tempo { get; set; } = "http://engineering-tempo:3200";
    public string Loki { get; set; } = "http://engineering-loki:3100";
    public string Broker { get; set; } = "http://rabbitmq:15672";
    public string BrokerUser { get; set; } = "";
    public string BrokerPassword { get; set; } = "";
    public string GitHubToken { get; set; } = "";
    public Dictionary<string, string> Databases { get; set; } = [];
}

public sealed record ExperimentRequest(string Kind, string Target, string Scenario, int Vus = 10,
    int DurationSeconds = 15, int RampSeconds = 0, int ArrivalRate = 10, int[]? Levels = null,
    double MaxErrorRate = .01, double MaxP95Ms = 1000, double MaxCpuPercent = 90,
    string Confirmation = "", int FaultSeconds = 5, int LatencyMs = 100, string[]? Models = null)
{
    public string? Validate(int maxVus)
    {
        if (Confirmation != "SYNTHETIC LAB ONLY") return "Confirm the isolated synthetic lab explicitly.";
        if (Kind is not ("load" or "capacity" or "chaos" or "ai-benchmark")) return "Unknown experiment kind.";
        if (Kind == "ai-benchmark" && (Models is not {Length: >0 and <=2} || Models.Distinct().Count()!=Models.Length || Models.Any(m=>m is not ("qwen3:4b" or "qwen2.5vl:3b")))) return "Benchmark models are not allowlisted.";
        if (Kind != "ai-benchmark" && Models is not null) return "Models apply only to synthetic benchmarks.";
        if (Target is not ("overview" or "candidates" or "health")) return "Target is not allowlisted.";
        if (Scenario is not ("constant" or "arrival" or "restart-candidate" or "interrupt-broker" or "interrupt-database" or "latency")) return "Scenario is not allowlisted.";
        if ((Kind == "chaos") != Scenario.Contains('-') && !(Kind == "chaos" && Scenario == "latency")) return "Scenario does not match experiment kind.";
        if (Kind != "chaos" && Scenario is not ("constant" or "arrival")) return "Invalid load scenario.";
        if (Vus < 1 || Vus > Math.Clamp(maxVus, 1, 10000) || DurationSeconds is < 5 or > 120 || RampSeconds is < 0 or > 20 || ArrivalRate is < 1 or > 1000) return "Resource budget exceeded.";
        if (!double.IsFinite(MaxErrorRate) || MaxErrorRate is < 0 or > 1 || !double.IsFinite(MaxP95Ms) || MaxP95Ms is < 1 or > 60000 || !double.IsFinite(MaxCpuPercent) || MaxCpuPercent is < 1 or > 100) return "Invalid SLO.";
        if (FaultSeconds is < 1 or > 15 || LatencyMs is < 1 or > 1000) return "Fault budget exceeded.";
        if (Kind == "capacity" && (Levels is not { Length: > 0 and <= 6 } || Levels.Any(x => x < 1 || x > maxVus) || !Levels.SequenceEqual(Levels.Distinct().Order()))) return "Capacity levels must be increasing, unique and within the server budget.";
        if (Kind != "capacity" && Levels is not null) return "Levels apply only to capacity exploration.";
        if ((Levels?.Length ?? 1) * (DurationSeconds + 2 * RampSeconds + 30) > 900) return "Total wall-clock budget exceeded.";
        return null;
    }
}

public sealed record RunSample(double ElapsedSeconds, long Requests, long Failures, long Timeouts, double Rps,
    double AverageMs, double P50Ms, double P95Ms, double P99Ms, double? CpuPercent, long? MemoryBytes,
    long? DatabaseConnections, long? QueueDepth);
public sealed record CapacityStep(int Vus, bool Passed, RunSample Metrics, Dictionary<string, long> HttpStatuses, string Outcome = "Passed");
public sealed record BenchmarkResult(string Model, string Dataset, string Status, string? ErrorCode,
    double DurationSeconds, int? ValidatedFacts, int? GoldMarkersMatched, int GoldMarkersTotal,
    double? NativeOutputTokensPerSecond, double? PeakCpuPercent, long? PeakMemoryBytes, string? ModelDigest);
public sealed record RunUpdate(string Status, int Progress, string Code, RunSample[] Samples,
    CapacityStep[] Steps, Dictionary<string, bool> Checks)
{
    public double? RecoverySeconds { get; init; }
    public BenchmarkResult[]? Benchmarks { get; init; }
    public bool IsValid() => Status is "Running" or "Succeeded" or "Failed" or "Cancelled" &&
        Progress is >= 0 and <= 100 && Code is ("Running" or "Completed" or "SloBreach" or "Cancelled" or "TelemetryInsufficient" or "RequestBudgetExceeded" or "ExecutionFailed" or "InterruptedRecovered" or "RecoveryFailed" or "InvalidLab" or "TotalTimeout") &&
        (RecoverySeconds is null || double.IsFinite(RecoverySeconds.Value) && RecoverySeconds >= 0) &&
        Samples is { Length: <= 1000 } && Steps is { Length: <= 6 } && Checks is { Count: <= 20 } &&
        Steps.All(s => s is not null && s.Metrics is not null) &&
        (Benchmarks is null || Benchmarks.Length <= 2 && Benchmarks.All(b=>b is not null && b.Model is ("qwen3:4b" or "qwen2.5vl:3b") && b.Dataset=="bilingual-v1" &&
            b.Status is ("Succeeded" or "Failed" or "NoFindings" or "Cancelled" or "ModelNotReady" or "Refused" or "OutcomeUnknown") &&
            (b.ErrorCode is null || System.Text.RegularExpressions.Regex.IsMatch(b.ErrorCode,"^AI\\.[A-Za-z.]{1,80}$")) &&
            double.IsFinite(b.DurationSeconds) && b.DurationSeconds >= 0 && b.GoldMarkersTotal==4 &&
            (b.ValidatedFacts is null || b.ValidatedFacts is >=0 and <=100) && (b.GoldMarkersMatched is null || b.GoldMarkersMatched is >=0 and <=4) &&
            (b.NativeOutputTokensPerSecond is null || double.IsFinite(b.NativeOutputTokensPerSecond.Value) && b.NativeOutputTokensPerSecond>=0) &&
            (b.PeakCpuPercent is null || double.IsFinite(b.PeakCpuPercent.Value) && b.PeakCpuPercent >= 0) &&
            (b.PeakMemoryBytes is null || b.PeakMemoryBytes >= 0) &&
            (b.ModelDigest is null || System.Text.RegularExpressions.Regex.IsMatch(b.ModelDigest,"^[a-f0-9]{12,64}$")))) &&
        Checks.Keys.All(k => System.Text.RegularExpressions.Regex.IsMatch(k, "^[a-zA-Z]{1,40}$")) &&
        Samples.Concat(Steps.Select(x => x.Metrics)).All(s => s is not null && s.Requests >= 0 && s.Failures >= 0 && s.Timeouts >= 0 &&
            new[] { s.ElapsedSeconds, s.Rps, s.AverageMs, s.P50Ms, s.P95Ms, s.P99Ms }.All(n => double.IsFinite(n) && n >= 0) &&
            (s.MemoryBytes is null || s.MemoryBytes >= 0) && (s.DatabaseConnections is null || s.DatabaseConnections >= 0) &&
            (s.QueueDepth is null || s.QueueDepth >= 0) &&
            (s.CpuPercent is null || double.IsFinite(s.CpuPercent.Value) && s.CpuPercent >= 0)) &&
        Steps.All(s => s.Vus is >= 1 and <= 10000 && s.Outcome is ("Passed" or "SloBreach" or "Inconclusive") && s.HttpStatuses is { Count: <= 70 } && s.HttpStatuses.All(k => int.TryParse(k.Key, out var n) && n is >= 0 and <= 599 && k.Value >= 0));
}
public sealed record EngineeringRun(Guid Id, string Kind, string Status, DateTime CreatedAtUtc,
    DateTime? FinishedAtUtc, bool CancellationRequested, JsonElement Request, JsonElement? Report);
public sealed record RunClaim(EngineeringRun Run, Guid Lease, bool RecoveryRequired);
public sealed record SourceResult(bool Available, object? Data, string? Error = null);
public sealed record ContainerSnapshot(string Name, string Service, string Image, string ImageId, string State,
    string Health, int RestartCount, DateTime StartedAtUtc);
public sealed record InventoryUpdate(ContainerSnapshot[] Containers)
{
    public bool IsValid() => Containers is { Length: <= 100 } && Containers.All(c => c is not null &&
        c.Name is { Length: <= 128 } && c.Name.StartsWith("nexushr", StringComparison.Ordinal) &&
        c.Service is { Length: <= 64 } && c.Image is { Length: <= 300 } &&
        c.ImageId is { Length: <= 80 } && c.RestartCount >= 0 &&
        c.State is ("created" or "running" or "paused" or "restarting" or "removing" or "exited" or "dead") &&
        c.Health is ("none" or "starting" or "healthy" or "unhealthy") &&
        new[]{c.Name,c.Service,c.Image,c.ImageId}.All(v=>System.Text.RegularExpressions.Regex.IsMatch(v,"^[A-Za-z0-9._:/@-]+$")));
}
