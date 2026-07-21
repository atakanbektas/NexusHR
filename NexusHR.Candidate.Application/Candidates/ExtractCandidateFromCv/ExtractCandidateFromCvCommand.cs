using MediatR;

namespace NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;

public sealed record ExtractCandidateFromCvCommand(
    string FileName,
    byte[] FileContent)
    : IRequest<ExtractCandidateFromCvResponse>;