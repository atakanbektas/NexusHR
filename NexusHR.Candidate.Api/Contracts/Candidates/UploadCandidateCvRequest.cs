using Microsoft.AspNetCore.Http;

namespace NexusHR.Candidate.Api.Contracts.Candidates;

public sealed class UploadCandidateCvRequest
{
    public IFormFile? File { get; init; }
}