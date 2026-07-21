using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Candidates.DownloadCandidateCv;

public sealed class DownloadCandidateCvQueryHandler(
    ICandidateDocumentRepository documentRepository,
    ICandidateDocumentStorage documentStorage)
    : IRequestHandler<
        DownloadCandidateCvQuery,
        DownloadCandidateCvResponse?>
{
    public async Task<DownloadCandidateCvResponse?> Handle(
        DownloadCandidateCvQuery request,
        CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetAsync(
            request.CandidateId,
            CandidateDocumentType.Cv,
            cancellationToken);

        if (document is null)
        {
            return null;
        }

        var content = await documentStorage.DownloadAsync(
            document.ObjectName,
            cancellationToken);

        return new DownloadCandidateCvResponse(
            content,
            document.ContentType,
            document.OriginalFileName);
    }
}