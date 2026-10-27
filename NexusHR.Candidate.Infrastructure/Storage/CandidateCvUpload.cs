using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Infrastructure.Persistence;
using CandidateEntity = NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Infrastructure.Storage;

internal sealed class CandidateCvUpload(CandidateDbContext db, ICandidateDocumentStorage storage,
    IServiceScopeFactory scopes, ILogger<CandidateCvUpload> logger) : ICandidateCvUpload
{
    public async Task SaveAsync(CandidateEntity candidate, CandidateDocument document, byte[] content, CancellationToken cancellationToken)
    {
        var intent = new CvUploadIntent { Id = document.Id, ObjectName = document.ObjectName,
            ReconcileAfterUtc = DateTime.UtcNow.AddMinutes(15) };
        db.Set<CvUploadIntent>().Add(intent);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(2));
            await storage.UploadAsync(document.ObjectName, content, document.ContentType, deadline.Token);
            await CommitAsync(candidate, document, intent, deadline.Token);
        }
        catch
        {
            // A fresh context checks the durable outcome before deleting anything.
            // If commit succeeded but acknowledgement was lost, the intent is absent.
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await CvUploadReconciler.CleanupAsync(scopes, intent.Id, timeout.Token);
            }
            catch (Exception cleanupError)
            {
                logger.LogWarning(cleanupError, "CV compensation deferred to reconciliation for {UploadId}", intent.Id);
            }
            throw;
        }
    }

    private async Task CommitAsync(CandidateEntity candidate, CandidateDocument document, CvUploadIntent intent, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The reconciler takes the same lock. An expired upload cannot commit after cleanup.
        var reservation = await db.Set<CvUploadIntent>().FromSqlInterpolated(
            $"SELECT * FROM cv_upload_intents WHERE \"Id\" = {intent.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (reservation is null) throw new InvalidOperationException("The upload reservation expired. Retry the upload.");
        candidate.MarkDocumentsPending();
        db.CandidateDocuments.Add(document);
        db.Set<CvUploadIntent>().Remove(reservation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
