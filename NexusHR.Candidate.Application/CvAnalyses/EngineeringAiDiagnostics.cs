using MediatR;
namespace NexusHR.Candidate.Application.CvAnalyses;

public sealed record AiAttemptSummary(int Sampled, bool Truncated, double? MeanSeconds, double? P95Seconds,
    double? EffectiveOutputTokensPerSecond, int Completed, int Retried, int Timeouts, int InvalidResponses);
public sealed record AiDiagnostics(bool Enabled, string Provider, string Model, Dictionary<string,int> States, AiAttemptSummary Attempts)
{
    public string? ModelDigest { get; init; }
    public double? NativeOutputTokensPerSecond { get; init; }
    public int FailedWithUnknownCode { get; init; }
    public Dictionary<string,int> ErrorCodes { get; init; } = [];
    public long? EvidenceProposed { get; init; }
    public long? EvidenceRejected { get; init; }
}
public interface IEngineeringAiReader { Task<AiDiagnostics> ReadAsync(CancellationToken ct); }
public sealed record GetEngineeringAiDiagnostics : IRequest<AiDiagnostics>;
public sealed class GetEngineeringAiDiagnosticsHandler(IEngineeringAiReader reader) : IRequestHandler<GetEngineeringAiDiagnostics,AiDiagnostics>
{
    public Task<AiDiagnostics> Handle(GetEngineeringAiDiagnostics request, CancellationToken ct) => reader.ReadAsync(ct);
}
