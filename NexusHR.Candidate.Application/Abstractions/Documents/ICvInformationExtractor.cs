namespace NexusHR.Candidate.Application.Abstractions.Documents;

public interface ICvInformationExtractor
{
    Task<ExtractedCandidateDraft> ExtractAsync(
        byte[] fileContent,
        CancellationToken cancellationToken);
}