using MediatR;

namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

public sealed record UploadCandidateCvCommand(
    Guid CandidateId,
    string FileName,
    string ContentType,
    byte[] FileContent,
    Guid? ReplacesDocumentId = null)
    : IRequest<UploadCandidateCvResponse>;
