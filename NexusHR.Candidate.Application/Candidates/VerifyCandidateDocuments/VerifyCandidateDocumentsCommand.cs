using MediatR;

namespace NexusHR.Candidate.Application.Candidates.VerifyCandidateDocuments;

public sealed record VerifyCandidateDocumentsCommand(
    Guid CandidateId)
    : IRequest<VerifyCandidateDocumentsResponse>;