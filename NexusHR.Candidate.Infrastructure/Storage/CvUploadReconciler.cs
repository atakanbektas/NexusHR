using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Infrastructure.Persistence;

namespace NexusHR.Candidate.Infrastructure.Storage;

internal sealed class CvUploadReconciler(IServiceScopeFactory scopes, ILogger<CvUploadReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
                var ids = await db.Set<CvUploadIntent>().AsNoTracking()
                    .Where(item => item.ReconcileAfterUtc < DateTime.UtcNow)
                    .OrderBy(item => item.ReconcileAfterUtc).Select(item => item.Id).Take(100).ToArrayAsync(stoppingToken);
                foreach (var id in ids) await CleanupAsync(scopes, id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "CV reconciliation failed; pending intents will be retried"); }
        }
    }

    internal static async Task CleanupAsync(IServiceScopeFactory scopes, Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var intent = await db.Set<CvUploadIntent>().FromSqlInterpolated(
            $"SELECT * FROM cv_upload_intents WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (intent is null) return;
        if (!await db.CandidateDocuments.AnyAsync(doc => doc.ObjectName == intent.ObjectName, ct))
            await scope.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>().DeleteAsync(intent.ObjectName, ct);
        db.Remove(intent);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
