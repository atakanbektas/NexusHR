namespace NexusHR.Candidate.Domain.CvAnalyses;

public enum CvAnalysisStatus { Queued, Running, Succeeded, Failed, Refused, OutcomeUnknown, Cancelled, NoFindings }

public sealed class CvAnalysis
{
    private CvAnalysis() { }
    public CvAnalysis(Guid candidateId, Guid documentId, string requestedBy, string key, string model,
        string provider, decimal reservation, DateTime now, bool externalProcessingAcknowledged = false)
    {
        if (candidateId == Guid.Empty || documentId == Guid.Empty || string.IsNullOrWhiteSpace(requestedBy) ||
            string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new ArgumentException("Invalid analysis identity.");
        Id = Guid.NewGuid(); CandidateId = candidateId; DocumentId = documentId; RequestedBy = requestedBy;
        IdempotencyKey = key; Model = model; Provider = provider; ReservedUsd = reservation;
        CreatedAtUtc = now; ReservationDay = DateOnly.FromDateTime(now); NextAttemptAtUtc = now;
        ExternalProcessingAcknowledged = externalProcessingAcknowledged;
    }
    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string RequestedBy { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string Provider { get; private set; } = "";
    public string SchemaVersion { get; private set; } = "facts-v1";
    public string PromptVersion { get; private set; } = "evidence-v1";
    public string ExtractorVersion { get; private set; } = "pdf-pages-v1";
    public string? ContentHash { get; private set; }
    public CvAnalysisStatus Status { get; private set; }
    public bool Delivered { get; private set; }
    public bool ExternalProcessingAcknowledged { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public Guid? LeaseId { get; private set; }
    public int AttemptCount { get; private set; }
    public bool CancellationRequested { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ResultJson { get; private set; }
    public decimal ReservedUsd { get; private set; }
    public DateOnly ReservationDay { get; private set; }
    public uint Version { get; private set; }
    public void MarkDelivered() => Delivered = true;
    public Guid Claim(DateTime now, TimeSpan lease)
    {
        if (Status != CvAnalysisStatus.Queued || NextAttemptAtUtc > now) throw new InvalidOperationException("Analysis is not claimable.");
        Status = CvAnalysisStatus.Running; AttemptCount++; LeaseId = Guid.NewGuid(); LeaseExpiresAtUtc = now + lease;
        return LeaseId.Value;
    }
    public void SetHash(Guid lease, string hash) { CheckLease(lease); ContentHash = hash; }
    public void Finish(Guid lease, CvAnalysisStatus status, string? result, string? error, DateTime now)
    {
        CheckLease(lease);
        if (status is CvAnalysisStatus.Queued or CvAnalysisStatus.Running) throw new ArgumentException("Terminal status required.");
        Status = status; ResultJson = result; ErrorCode = error; FinishedAtUtc = now; LeaseId = null; LeaseExpiresAtUtc = null;
    }
    public void RequestCancellation(DateTime now)
    {
        if (Status is not (CvAnalysisStatus.Queued or CvAnalysisStatus.Running)) throw new InvalidOperationException("Analysis already finished.");
        CancellationRequested = true;
        if (Status == CvAnalysisStatus.Queued) { Status = CvAnalysisStatus.Cancelled; FinishedAtUtc = now; }
    }
    public void Retry(decimal reservation, DateTime now, TimeSpan delay, bool acknowledgeUnknown)
    {
        if (Status != CvAnalysisStatus.Failed && Status != CvAnalysisStatus.NoFindings && !(Status == CvAnalysisStatus.OutcomeUnknown && acknowledgeUnknown))
            throw new InvalidOperationException("Analysis cannot be retried without resolving its outcome.");
        Status = CvAnalysisStatus.Queued; CancellationRequested = false; ErrorCode = null; FinishedAtUtc = null;
        ReservedUsd = reservation; ReservationDay = DateOnly.FromDateTime(now); NextAttemptAtUtc = now + delay;
    }
    private void CheckLease(Guid lease)
    {
        if (Status != CvAnalysisStatus.Running || LeaseId != lease) throw new InvalidOperationException("Analysis lease lost.");
    }
}

public sealed class CvAnalysisAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }
    public int Number { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string Outcome { get; set; } = "Running";
    public string? ProviderRequestId { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal ReservedUsd { get; set; }
    public decimal? UsageCostUsd { get; set; }
    public string PriceVersion { get; set; } = "";
    public decimal InputRate { get; set; }
    public decimal OutputRate { get; set; }
}

public sealed class CvAnalysisReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }
    public string Reviewer { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public int Revision { get; set; }
}

public sealed class AiDailyBudget
{
    public DateOnly Day { get; set; }
    // Settled usage plus outstanding/worst-case unknown reservations, in USD.
    public decimal CommittedUsd { get; set; }
}

public sealed class AiDailyQuota
{
    public DateOnly Day { get; set; }
    public string Subject { get; set; } = "";
    public int Reservations { get; set; }
}
