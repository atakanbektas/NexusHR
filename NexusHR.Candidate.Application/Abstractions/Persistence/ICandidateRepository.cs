using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Application.Abstractions.Persistence;

public interface ICandidateRepository
{
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken);

    Task AddAsync(
        CandidateEntity candidate,
        CancellationToken cancellationToken);
}