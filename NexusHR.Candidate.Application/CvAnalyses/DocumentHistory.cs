using MediatR;
using NexusHR.Candidate.Application.Candidates.DownloadCandidateCv;

namespace NexusHR.Candidate.Application.CvAnalyses;

public sealed record CvDocumentVersion(Guid Id, int Revision, bool IsCurrent, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc);
public interface ICvDocumentHistory
{
    Task<IReadOnlyList<CvDocumentVersion>> ListAsync(Guid candidateId, CancellationToken ct);
    Task<DownloadCandidateCvResponse> DownloadAsync(Guid candidateId, Guid documentId, CancellationToken ct);
}
public sealed record ListCvDocumentVersions(Guid CandidateId) : IRequest<IReadOnlyList<CvDocumentVersion>>;
public sealed record DownloadCvDocumentVersion(Guid CandidateId, Guid DocumentId) : IRequest<DownloadCandidateCvResponse>;
internal sealed class DocumentHistoryHandlers(ICvDocumentHistory store) :
    IRequestHandler<ListCvDocumentVersions, IReadOnlyList<CvDocumentVersion>>, IRequestHandler<DownloadCvDocumentVersion, DownloadCandidateCvResponse>
{
    public Task<IReadOnlyList<CvDocumentVersion>> Handle(ListCvDocumentVersions r, CancellationToken ct) => store.ListAsync(r.CandidateId, ct);
    public Task<DownloadCandidateCvResponse> Handle(DownloadCvDocumentVersion r, CancellationToken ct) => store.DownloadAsync(r.CandidateId, r.DocumentId, ct);
}
