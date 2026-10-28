using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Contracts.Candidates;

namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

internal sealed class CvAnalysisStore(CandidateDbContext db, IPublishEndpoint publisher, CvAnalysisOptions options, ICvAnalysisProvider provider) : ICvAnalysisStore
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // A transaction-scoped lock serializes only short AI state/budget writes across replicas.
    internal static Task LockAsync(CandidateDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724810935)", ct);

    public async Task<AnalysisView> RequestAsync(Guid candidateId, Guid documentId, string user, string key, bool consent, CancellationToken ct)
    {
        ValidateUser(user);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new CvAnalysisException(400, "AI.InvalidIdempotencyKey");
        // Readiness performs no CV transfer and never holds a database transaction open.
        // An acknowledged idempotent replay must remain readable even during an outage.
        var replay = await db.Set<CvAnalysis>().AsNoTracking().SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.RequestedBy == user && x.IdempotencyKey == key, ct);
        if (replay is null) { RequireEnabled(); await RequireReadyAsync(ct); }
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(db, ct);
        var existing = await db.Set<CvAnalysis>().SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.RequestedBy == user && x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.DocumentId != documentId) throw new CvAnalysisException(409, "AI.IdempotencyConflict");
            return await ViewAsync(existing, ct);
        }
        RequireEnabled();
        if (!options.IsLocal && options.AllowExternalProcessing && !consent) throw new CvAnalysisException(400, "AI.ProcessingAcknowledgementRequired");
        if (!await db.CandidateDocuments.AnyAsync(x => x.Id == documentId && x.CandidateId == candidateId && x.Type == CandidateDocumentType.Cv, ct))
            throw new CvAnalysisException(404, "Candidate.CvNotFound");
        if (await db.Set<CvAnalysis>().AnyAsync(x => x.DocumentId == documentId && (x.Status == CvAnalysisStatus.Queued || x.Status == CvAnalysisStatus.Running), ct))
            throw new CvAnalysisException(409, "AI.AnalysisAlreadyActive");
        var now = DateTime.UtcNow;
        await ReserveAsync(user, now, ct);
        var run = new CvAnalysis(candidateId, documentId, user, key, options.Model, options.Provider, options.Reservation, now, consent);
        db.Add(run);
        await publisher.Publish(new CvAnalysisRequested(run.Id), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await ViewAsync(run, ct);
    }

    internal void RequireEnabled()
    {
        if (!options.Enabled) throw new CvAnalysisException(503, "AI.Disabled");
    }
    private async Task RequireReadyAsync(CancellationToken ct)
    {
        if (await provider.GetUnavailableCodeAsync(options.Model, ct) is { } error) throw new CvAnalysisException(503, error);
    }
    internal async Task ReserveAsync(string user, DateTime now, CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(now);
        var quota = await db.Set<AiDailyQuota>().FindAsync([day, user], ct);
        if (quota?.Reservations >= options.RequestsPerUserPerDay) throw new CvAnalysisException(429, "AI.UserQuotaExceeded");
        if (options.IsLocal)
        {
            if (quota is null) { quota = new() { Day = day, Subject = user }; db.Add(quota); }
            quota.Reservations++;
            return; // Local compute still has quotas; it does not reserve paid API funds.
        }
        var budget = await db.Set<AiDailyBudget>().FindAsync([day], ct);
        if (budget is null) { budget = new() { Day = day }; db.Add(budget); }
        if (budget.CommittedUsd + options.Reservation > options.DailyBudgetUsd) throw new CvAnalysisException(429, "AI.DailyBudgetExceeded");
        if (quota is null) { quota = new() { Day = day, Subject = user }; db.Add(quota); }
        quota.Reservations++;
        budget.CommittedUsd += options.Reservation;
    }
    internal static async Task SettleAsync(CandidateDbContext db, CvAnalysis run, decimal actual, CancellationToken ct)
    {
        if (run.Provider == "Ollama") return;
        var budget = await db.Set<AiDailyBudget>().SingleAsync(x => x.Day == run.ReservationDay, ct);
        budget.CommittedUsd = Math.Max(0, budget.CommittedUsd - run.ReservedUsd + actual);
    }
    public async Task<AnalysisHistory> ListAsync(Guid candidateId, int page, CancellationToken ct)
    {
        if (!await db.Candidates.AnyAsync(x => x.Id == candidateId, ct)) throw new CvAnalysisException(404, "Candidate.NotFound");
        if (page is < 1 or > 1000000) throw new CvAnalysisException(400, "AI.InvalidPage");
        var query = db.Set<CvAnalysis>().AsNoTracking().Where(x => x.CandidateId == candidateId);
        var runs = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        var views = new List<AnalysisView>(); foreach (var run in runs) views.Add(await ViewAsync(run, ct));
        var unavailable = options.Enabled ? await provider.GetUnavailableCodeAsync(options.Model, ct) : null;
        return new(new(options.Enabled, !options.IsLocal && options.AllowExternalProcessing, options.Provider, options.Reservation, unavailable is null, unavailable),
            await db.CandidateDocuments.Where(x => x.CandidateId == candidateId && x.Type == CandidateDocumentType.Cv).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct),
            views, page, await query.CountAsync(ct));
    }
    public async Task<AnalysisView> GetAsync(Guid candidateId, Guid id, CancellationToken ct) => await ViewAsync(await FindAsync(candidateId, id, ct), ct);
    internal async Task<CvAnalysis> FindAsync(Guid candidateId, Guid id, CancellationToken ct) =>
        await db.Set<CvAnalysis>().SingleOrDefaultAsync(x => x.Id == id && x.CandidateId == candidateId, ct) ?? throw new CvAnalysisException(404, "AI.NotFound");

    public async Task<AnalysisView> MutateAsync(Guid candidateId, Guid id, string user, uint version, string action, bool acknowledgeUnknown, string? decision, string? notes, CancellationToken ct)
    {
        ValidateUser(user);
        if (action == "retry") { RequireEnabled(); await RequireReadyAsync(ct); }
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(db, ct);
        var run = await FindAsync(candidateId, id, ct);
        if (run.Version != version) throw new CvAnalysisException(409, "AI.VersionConflict");
        var now = DateTime.UtcNow;
        if (action == "cancel")
        {
            var queued = run.Status == CvAnalysisStatus.Queued;
            run.RequestCancellation(now);
            if (queued) await SettleAsync(db, run, 0, ct);
        }
        else if (action == "retry")
        {
            RequireEnabled();
            if (run.RequestedBy != user) throw new CvAnalysisException(403, "AI.RetryRequiresOriginalRequester");
            if (run.AttemptCount >= options.MaxAttempts) throw new CvAnalysisException(409, "AI.AttemptLimit");
            if (run.Model != options.Model || run.Provider != options.Provider) throw new CvAnalysisException(409, "AI.ModelChangedCreateNewAnalysis");
            if (await db.Set<CvAnalysis>().AnyAsync(x => x.Id != id && x.DocumentId == run.DocumentId &&
                (x.Status == CvAnalysisStatus.Running || x.Status == CvAnalysisStatus.Queued), ct)) throw new CvAnalysisException(409, "AI.AnalysisAlreadyActive");
            await ReserveAsync(user, now, ct);
            run.Retry(options.Reservation, now, TimeSpan.Zero, acknowledgeUnknown);
            await publisher.Publish(new CvAnalysisRequested(run.Id), ct);
        }
        else if (action == "review")
        {
            if (run.Status != CvAnalysisStatus.Succeeded) throw new CvAnalysisException(409, "AI.ResultNotAvailable");
            if (decision is not ("approve" or "reject" or "correct") || notes?.Length > 4000 ||
                (decision == "correct" && string.IsNullOrWhiteSpace(notes))) throw new CvAnalysisException(400, "AI.InvalidReview");
            var revision = await db.Set<CvAnalysisReview>().CountAsync(x => x.AnalysisId == id, ct) + 1;
            db.Add(new CvAnalysisReview { AnalysisId = id, Reviewer = user, Decision = decision, Notes = notes ?? "", CreatedAtUtc = now, Revision = revision });
            // Force a parent update so stale review versions conflict even when only a child is added.
            db.Entry(run).Property(x => x.FinishedAtUtc).IsModified = true;
        }
        else throw new CvAnalysisException(400, "AI.InvalidAction");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await ViewAsync(run, ct);
    }
    private static void ValidateUser(string user)
    {
        if (string.IsNullOrWhiteSpace(user) || user.Length > 200) throw new CvAnalysisException(403, "AI.SubjectRequired");
    }
    private async Task<AnalysisView> ViewAsync(CvAnalysis r, CancellationToken ct) => new(r.Id, r.DocumentId, r.Status.ToString(), r.Version,
        r.CancellationRequested, r.CreatedAtUtc, r.ErrorCode, r.Model, r.ContentHash,
        r.ResultJson is null ? null : JsonSerializer.Deserialize<CvAnalysisResult>(r.ResultJson, Json),
        await db.Set<CvAnalysisReview>().Where(x => x.AnalysisId == r.Id).OrderByDescending(x => x.Revision)
            .Select(x => new ReviewView(x.Reviewer, x.Decision, x.Notes, x.CreatedAtUtc, x.Revision)).ToListAsync(ct),
        await db.Set<CvAnalysisAttempt>().Where(x => x.AnalysisId == r.Id).OrderBy(x => x.Number)
            .Select(x => new AttemptView(x.Number, x.Outcome, x.InputTokens, x.OutputTokens, x.ReservedUsd, x.UsageCostUsd, x.PriceVersion)).ToListAsync(ct), r.Provider);
}
