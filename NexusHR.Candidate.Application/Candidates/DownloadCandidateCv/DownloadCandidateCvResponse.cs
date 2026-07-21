namespace NexusHR.Candidate.Application.Candidates.DownloadCandidateCv;

public sealed record DownloadCandidateCvResponse(
    byte[] Content,
    string ContentType,
    string FileName);