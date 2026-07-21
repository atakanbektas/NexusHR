using MediatR;

namespace NexusHR.Candidate.Application.Candidates.DownloadCandidateCv;

public sealed record DownloadCandidateCvQuery(
    Guid CandidateId)
    : IRequest<DownloadCandidateCvResponse?>;