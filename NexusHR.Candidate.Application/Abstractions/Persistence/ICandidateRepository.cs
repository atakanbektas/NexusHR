using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Application.Abstractions.Persistence;

public interface ICandidateRepository
{
    Task<CandidateEntity?> GetByIdAsync(
    Guid candidateId,
    CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken);

    Task AddAsync(
        CandidateEntity candidate,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CandidateEntity>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        CancellationToken cancellationToken);
}