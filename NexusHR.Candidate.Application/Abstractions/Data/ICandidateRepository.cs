using System;
using System.Collections.Generic;
using System.Text;

namespace NexusHR.Candidate.Application.Abstractions.Data
{
    public interface ICandidateRepository
    {
        Task<bool> EmailExistsAsync(
            string email,
            CancellationToken cancellationToken);

        Task AddAsync(
            Domain.Candidates.Candidate candidate,
            CancellationToken cancellationToken);
    }
}
