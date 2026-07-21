using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Abstractions.Persistence;

public interface ICandidateDocumentRepository
{
    Task<bool> ExistsAsync(
        Guid candidateId,
        CandidateDocumentType type,
        CancellationToken cancellationToken);

    Task<CandidateDocument?> GetAsync(
        Guid candidateId,
        CandidateDocumentType type,
        CancellationToken cancellationToken);

    Task AddAsync(
        CandidateDocument document,
        CancellationToken cancellationToken);
}