using NexusHR.Candidate.Domain.Candidates;
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

    Task<bool> EmailExistsAsync(
        string email,
        Guid excludedCandidateId,
        CancellationToken cancellationToken);

    Task AddAsync(
        CandidateEntity candidate,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        CandidateEntity candidate,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CandidateEntity>> GetPageAsync(
        int skip,
        int take,
        string? search,
        CandidateStatus? status,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        string? search,
        CandidateStatus? status,
        CancellationToken cancellationToken);
}
