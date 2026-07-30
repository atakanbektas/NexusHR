using NexusHR.Hiring.Domain.HiringProcesses;

namespace NexusHR.Hiring.Application.Abstractions.Persistence;

public interface IHiringProcessRepository
{
    Task<HiringProcess?> GetByIdAsync(
        Guid hiringProcessId,
        CancellationToken cancellationToken);

    Task<HiringProcess?> GetActiveByCandidateIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveProcessAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    Task AddAsync(
        HiringProcess hiringProcess,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        HiringProcess hiringProcess,
        CancellationToken cancellationToken);
}