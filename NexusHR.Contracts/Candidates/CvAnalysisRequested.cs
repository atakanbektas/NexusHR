namespace NexusHR.Contracts.Candidates;

// Identifiers only: no CV text, contact data or object storage URL on the broker.
public sealed record CvAnalysisRequested(Guid AnalysisId);
