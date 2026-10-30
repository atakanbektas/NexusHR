using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;

namespace NexusHR.Candidate.Infrastructure.CvAnalyses;

internal sealed class CvAnalysisWorker(IServiceScopeFactory scopes, CvAnalysisOptions options, ILogger<CvAnalysisWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverAsync(scopes, stoppingToken);
                // Bounded local concurrency; leases also prevent duplicate work across replicas.
                await Task.WhenAll(ProcessNextAsync(scopes, options, stoppingToken), ProcessNextAsync(scopes, options, stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogWarning("AI worker cycle failed ({ErrorType}); durable jobs will be recovered", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    internal static async Task RecoverAsync(IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CvAnalysisStore.LockAsync(db, ct);
        var now = DateTime.UtcNow;
        var expired = await db.Set<CvAnalysis>().Where(x => x.Status == CvAnalysisStatus.Running && x.LeaseExpiresAtUtc < now).Take(100).ToListAsync(ct);
        foreach (var run in expired)
        {
            var attempt = await db.Set<CvAnalysisAttempt>().SingleAsync(x => x.AnalysisId == run.Id && x.Number == run.AttemptCount, ct);
            var unknown = attempt.DispatchedAtUtc.HasValue;
            run.Finish(run.LeaseId!.Value, unknown ? CvAnalysisStatus.OutcomeUnknown : CvAnalysisStatus.Failed, null,
                unknown ? "AI.OutcomeUnknown" : "AI.WorkerInterruptedBeforeDispatch", now);
            attempt.Outcome = run.Status.ToString(); attempt.FinishedAtUtc = now;
            if (!unknown) { attempt.UsageCostUsd = 0; await CvAnalysisStore.SettleAsync(db, run, 0, ct); }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    internal static async Task<bool> ProcessNextAsync(IServiceScopeFactory scopes, CvAnalysisOptions options, CancellationToken ct)
    {
        if (!options.Enabled) return false;
        Guid id, lease; int number;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct); await CvAnalysisStore.LockAsync(db, ct);
            var now = DateTime.UtcNow;
            var run = await db.Set<CvAnalysis>().Where(x => x.Status == CvAnalysisStatus.Queued && x.NextAttemptAtUtc <= now &&
                (x.Delivered || x.CreatedAtUtc < now.AddSeconds(-30))).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (run is null) return false;
            id = run.Id; lease = run.Claim(now, TimeSpan.FromSeconds(options.TimeoutSeconds + 60)); number = run.AttemptCount;
            db.Add(new CvAnalysisAttempt { AnalysisId = id, Number = number, StartedAtUtc = now,
                ReservedUsd = run.ReservedUsd, PriceVersion = options.PriceVersion, InputRate = options.InputUsdPerMillion, OutputRate = options.OutputUsdPerMillion });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }

        CvProviderResponse? response = null; string? error = null;
        var status = CvAnalysisStatus.Failed; var dispatched = false; CvProviderException? retry = null;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
            var run = await db.Set<CvAnalysis>().SingleAsync(x => x.Id == id, ct);
            var provider = scope.ServiceProvider.GetRequiredService<ICvAnalysisProvider>();
            if (provider.RequiresExternalProcessing && (!options.AllowExternalProcessing || !run.ExternalProcessingAcknowledged))
                throw new CvProviderException("AI.ExternalProcessingDisabled");
            if (provider.RequiresExternalProcessing && string.IsNullOrWhiteSpace(options.ApiKey)) throw new CvProviderException("AI.ProviderNotConfigured");
            if (run.ReservedUsd < options.Reservation || run.Model != options.Model) throw new CvProviderException("AI.ConfigurationChangedRetryRequired");
            var document = await db.CandidateDocuments.SingleAsync(x => x.Id == run.DocumentId && x.CandidateId == run.CandidateId, ct);
            var bytes = await scope.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>().DownloadAsync(document.ObjectName, ct);
            var pages = await scope.ServiceProvider.GetRequiredService<ICvTextExtractor>().ExtractPagesAsync(bytes, ct);
            if (pages.Sum(x => (long)x.Text.Length) > options.MaxInputCharacters) throw new InvalidDataException("AI input limit exceeded.");
            // Best-effort contact minimization; not a claim of anonymization. Preserve offsets by replacing with spaces.
            var candidate = await db.Candidates.SingleAsync(x => x.Id == run.CandidateId, ct);
            pages = pages.Select(p => new CvTextPage(p.Number, Redact(p.Text, candidate.FirstName, candidate.LastName, candidate.Email, candidate.PhoneNumber))).ToArray();
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                await CvAnalysisStore.LockAsync(db, ct); await db.Entry(run).ReloadAsync(ct);
                if (run.LeaseId != lease) return true;
                if (run.CancellationRequested)
                {
                    run.Finish(lease, CvAnalysisStatus.Cancelled, null, null, DateTime.UtcNow);
                    var cancelled = await db.Set<CvAnalysisAttempt>().SingleAsync(x => x.AnalysisId == id && x.Number == number, ct);
                    cancelled.Outcome = "Cancelled"; cancelled.UsageCostUsd = 0; cancelled.FinishedAtUtc = DateTime.UtcNow;
                    await CvAnalysisStore.SettleAsync(db, run, 0, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
                }
                // Queues can survive an overnight outage. Never spend yesterday's
                // reservation against today's provider usage without a fresh budget check.
                if (run.ReservationDay != DateOnly.FromDateTime(DateTime.UtcNow))
                    throw new CvProviderException("AI.ReservationExpiredRetryRequired");
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                document.RecordContentHash(hash); // Lazy additive backfill for existing immutable CVs.
                run.SetHash(lease, hash);
                var attempt = await db.Set<CvAnalysisAttempt>().SingleAsync(x => x.AnalysisId == id && x.Number == number, ct);
                attempt.DispatchedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            }
            dispatched = true;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            response = await provider.AnalyzeAsync(pages, run.Model, deadline.Token);
            if (response.InputTokens < 0 || response.OutputTokens < 0) throw new InvalidDataException("Invalid usage.");
            EvidenceValidator.Validate(response.Result, pages);
            status = CvAnalysisStatus.Succeeded;
        }
        catch (CvProviderException ex)
        {
            error = ex.Code;
            status = ex.Code == "AI.Refused" ? CvAnalysisStatus.Refused : ex.Code == "AI.OutcomeUnknown" ? CvAnalysisStatus.OutcomeUnknown : CvAnalysisStatus.Failed;
            if (ex.SafeToRetry) retry = ex;
        }
        catch (InvalidDataException) { error = "AI.InvalidInputOrEvidence"; }
        catch (Exception) { status = dispatched ? CvAnalysisStatus.OutcomeUnknown : CvAnalysisStatus.Failed; error = dispatched ? "AI.OutcomeUnknown" : "AI.InputUnavailable"; }

        // Fresh context, independent of caller cancellation, to durably record the external outcome.
        using var finishDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var finishCt = finishDeadline.Token;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(finishCt); await CvAnalysisStore.LockAsync(db, finishCt);
            var run = await db.Set<CvAnalysis>().SingleAsync(x => x.Id == id, finishCt);
            if (run.LeaseId != lease) return true;
            var attempt = await db.Set<CvAnalysisAttempt>().SingleAsync(x => x.AnalysisId == id && x.Number == number, finishCt);
            // Unknown/invalid provider output keeps its full reservation unless trustworthy usage was returned.
            decimal? cost = response is { InputTokens: >= 0, OutputTokens: >= 0 } ? (response.InputTokens * attempt.InputRate + response.OutputTokens * attempt.OutputRate) / 1_000_000m :
                !dispatched || retry is not null ? 0 : null;
            if (run.CancellationRequested && status == CvAnalysisStatus.Succeeded) status = CvAnalysisStatus.Cancelled;
            run.Finish(lease, status, status == CvAnalysisStatus.Succeeded ? JsonSerializer.Serialize(response!.Result, CvAnalysisStore.Json) : null, error, DateTime.UtcNow);
            attempt.Outcome = status.ToString(); attempt.FinishedAtUtc = DateTime.UtcNow; attempt.ProviderRequestId = response?.RequestId;
            attempt.InputTokens = response?.InputTokens; attempt.OutputTokens = response?.OutputTokens; attempt.UsageCostUsd = cost;
            if (cost.HasValue) await CvAnalysisStore.SettleAsync(db, run, cost.Value, finishCt);
            if (retry is not null && (retry.RetryAfter is null || retry.RetryAfter <= TimeSpan.FromMinutes(5)) && number < options.MaxAttempts && !run.CancellationRequested)
            {
                try
                {
                    await scope.ServiceProvider.GetRequiredService<CvAnalysisStore>().ReserveAsync(run.RequestedBy, DateTime.UtcNow, finishCt);
                    var delay = retry.RetryAfter ?? TimeSpan.FromSeconds(5 * number + Random.Shared.Next(1, 4));
                    run.Retry(options.Reservation, DateTime.UtcNow, delay, false);
                }
                catch (CvAnalysisException) { /* Terminal failure remains visible when quota cannot fund a retry. */ }
            }
            await db.SaveChangesAsync(finishCt); await tx.CommitAsync(finishCt);
        }
        return true;
    }

    private static string Redact(string text, params string[] identifiers)
    {
        foreach (var value in identifiers.Where(x => x.Length >= 2).OrderByDescending(x => x.Length))
            text = Regex.Replace(text, Regex.Escape(value), m => new string(' ', m.Length), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, @"[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}|(?:\+?\d[\d ()-]{8,}\d)", m => new string(' ', m.Length), RegexOptions.None, TimeSpan.FromSeconds(1));
        return text;
    }
}
