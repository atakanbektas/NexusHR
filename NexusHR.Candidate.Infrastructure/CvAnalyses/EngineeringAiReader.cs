using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

public sealed class EngineeringAiReader(CandidateDbContext db, CvAnalysisOptions options) : IEngineeringAiReader
{
    public async Task<AiDiagnostics> ReadAsync(CancellationToken ct)
    {
        var counts = await db.Set<CvAnalysis>().AsNoTracking().GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        var since = DateTime.UtcNow.AddHours(-24);
        var attempts = await db.Set<CvAnalysisAttempt>().AsNoTracking().Where(x => x.StartedAtUtc >= since)
            .OrderByDescending(x => x.StartedAtUtc).Select(x => new { x.StartedAtUtc, x.FinishedAtUtc, x.OutputTokens, x.Number, x.Outcome,
                x.ErrorCode,x.FailureStage,x.NativeOutputTokensPerSecond,x.EvidenceProposed,x.EvidenceRejected }).Take(5001).ToListAsync(ct);
        var data = attempts.Take(5000).ToArray();
        var durations = data.Where(x => x.FinishedAtUtc.HasValue).Select(x => (x.FinishedAtUtc!.Value-x.StartedAtUtc).TotalSeconds).Order().ToArray();
        var completed = data.Where(x => x.FinishedAtUtc.HasValue && x.OutputTokens.HasValue).ToArray();
        var seconds = completed.Sum(x => (x.FinishedAtUtc!.Value-x.StartedAtUtc).TotalSeconds);
        var nativeRates = data.Where(x=>x.NativeOutputTokensPerSecond.HasValue).Select(x=>x.NativeOutputTokensPerSecond!.Value).ToArray();
        var evidence=data.Where(x=>x.EvidenceProposed.HasValue).ToArray();
        var digest = await (from a in db.Set<CvAnalysisAttempt>() join r in db.Set<CvAnalysis>() on a.AnalysisId equals r.Id
            where r.Provider == options.Provider && r.Model == options.Model && a.ProviderRequestId != null
            orderby a.StartedAtUtc descending select a.ProviderRequestId).FirstOrDefaultAsync(ct);
        return new(options.Enabled, options.Provider, options.Model, counts.ToDictionary(x => x.status.ToString(), x => x.count),
            new(data.Length, attempts.Count > 5000, durations.Length == 0 ? null : durations.Average(), durations.Length == 0 ? null : durations[(int)Math.Ceiling(.95 * durations.Length)-1],
                seconds <= 0 ? null : completed.Sum(x => (long)x.OutputTokens!.Value)/seconds, completed.Length, data.Count(x => x.Number > 1),
                data.Count(x => x.ErrorCode == "AI.Timeout"), data.Count(x => x.ErrorCode is "AI.InvalidResponse" or "AI.InvalidInputOrEvidence"))) {
            ModelDigest = digest is not null && System.Text.RegularExpressions.Regex.IsMatch(digest,"^ollama:[a-f0-9]{12,64}$") ? digest[7..] : null,
            NativeOutputTokensPerSecond = nativeRates.Length==0?null:nativeRates.Average(),
            ErrorCodes = data.Where(x=>x.ErrorCode is not null && System.Text.RegularExpressions.Regex.IsMatch(x.ErrorCode,"^AI\\.[A-Za-z.]+$")).GroupBy(x=>x.ErrorCode!).ToDictionary(g=>g.Key,g=>g.Count()),
            FailedWithUnknownCode = data.Count(x=>x.Outcome=="Failed" && x.ErrorCode is null),
            EvidenceProposed = evidence.Length==0?null:evidence.Sum(x=>(long)x.EvidenceProposed!.Value),
            EvidenceRejected = evidence.Length==0?null:evidence.Sum(x=>(long)(x.EvidenceRejected??0))
        };
    }
}
