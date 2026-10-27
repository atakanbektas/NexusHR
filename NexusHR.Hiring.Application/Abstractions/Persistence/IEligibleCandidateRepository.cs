using NexusHR.Hiring.Domain.EligibleCandidates;

namespace NexusHR.Hiring.Application.Abstractions.Persistence;

public interface IEligibleCandidateRepository
{
    Task<EligibleCandidate?> GetByCandidateIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    Task AddAsync(
        EligibleCandidate candidate,
        CancellationToken cancellationToken);
}