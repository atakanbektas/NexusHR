using NexusHR.Candidate.Domain.Candidates;
using CandidateEntity = NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Application.Abstractions.Storage;

public interface ICandidateCvUpload
{
    Task SaveAsync(CandidateEntity candidate, CandidateDocument document, byte[] content, CancellationToken cancellationToken);
}
