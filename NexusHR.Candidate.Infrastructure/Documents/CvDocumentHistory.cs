using Microsoft.EntityFrameworkCore;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Application.Candidates.DownloadCandidateCv;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Infrastructure.Persistence;

namespace NexusHR.Candidate.Infrastructure.Documents;

internal sealed class CvDocumentHistory(CandidateDbContext db, ICandidateDocumentStorage storage) : ICvDocumentHistory
{
    public async Task<IReadOnlyList<CvDocumentVersion>> ListAsync(Guid candidateId, CancellationToken ct) =>
        await db.CandidateDocuments.AsNoTracking().Where(x => x.CandidateId == candidateId && x.Type == CandidateDocumentType.Cv)
            .OrderByDescending(x => x.Revision).Take(100)
            .Select(x => new CvDocumentVersion(x.Id, x.Revision, x.IsCurrent, x.OriginalFileName, x.ContentType, x.SizeBytes, x.UploadedAtUtc)).ToListAsync(ct);

    public async Task<DownloadCandidateCvResponse> DownloadAsync(Guid candidateId, Guid documentId, CancellationToken ct)
    {
        var doc = await db.CandidateDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.CandidateId == candidateId && x.Type == CandidateDocumentType.Cv, ct)
            ?? throw new CvAnalysisException(404, "Candidate.CvNotFound");
        return new(await storage.DownloadAsync(doc.ObjectName, ct), doc.ContentType, doc.OriginalFileName);
    }
}
