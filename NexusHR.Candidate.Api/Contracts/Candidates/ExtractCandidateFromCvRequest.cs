using Microsoft.AspNetCore.Http;

namespace NexusHR.Candidate.Api.Contracts.Candidates;

public sealed class ExtractCandidateFromCvRequest
{
    public IFormFile? File { get; init; }
}