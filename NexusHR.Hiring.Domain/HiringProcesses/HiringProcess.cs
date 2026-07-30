namespace NexusHR.Hiring.Domain.HiringProcesses;

public sealed class HiringProcess
{
    private HiringProcess()
    {
    }

    public HiringProcess(
        Guid id,
        Guid candidateId,
        string positionTitle,
        string department,
        EmploymentType employmentType)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "İşe alım süreci kimliği boş olamaz.",
                nameof(id));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException(
                "Aday kimliği boş olamaz.",
                nameof(candidateId));
        }

        if (string.IsNullOrWhiteSpace(positionTitle))
        {
            throw new ArgumentException(
                "Pozisyon adı boş olamaz.",
                nameof(positionTitle));
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            throw new ArgumentException(
                "Departman adı boş olamaz.",
                nameof(department));
        }

        if (!Enum.IsDefined(employmentType))
        {
            throw new ArgumentException(
                "Geçerli bir istihdam türü seçilmelidir.",
                nameof(employmentType));
        }

        Id = id;
        CandidateId = candidateId;
        PositionTitle = positionTitle.Trim();
        Department = department.Trim();
        EmploymentType = employmentType;
        Status = HiringProcessStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CandidateId { get; private set; }

    public Guid? EmployeeId { get; private set; }

    public string PositionTitle { get; private set; } = null!;

    public string Department { get; private set; } = null!;

    public EmploymentType EmploymentType { get; private set; }

    public HiringProcessStatus Status { get; private set; }

    public decimal? GrossSalary { get; private set; }

    public string? Currency { get; private set; }

    public DateOnly? ProposedStartDate { get; private set; }

    public DateTime? OfferExpiresAtUtc { get; private set; }

    public DateTime? OfferSentAtUtc { get; private set; }

    public DateTime? OfferRespondedAtUtc { get; private set; }

    public string? OfferResponseTokenHash
    {
        get;
        private set;
    }

    public DateTime? OfferResponseTokenUsedAtUtc
    {
        get;
        private set;
    }

    public string? RejectionReason { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public void PrepareOffer(
        decimal grossSalary,
        string currency,
        DateOnly proposedStartDate,
        DateTime offerExpiresAtUtc)
    {
        if (Status is not HiringProcessStatus.Draft
            and not HiringProcessStatus.OfferPrepared)
        {
            throw new InvalidOperationException(
                "Teklif yalnızca taslak veya teklif hazırlanmış durumunda düzenlenebilir.");
        }

        if (grossSalary <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grossSalary),
                "Brüt maaş sıfırdan büyük olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException(
                "Para birimi boş olamaz.",
                nameof(currency));
        }

        var normalizedCurrency =
            currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException(
                "Para birimi üç karakterli ISO kodu olmalıdır.",
                nameof(currency));
        }

        if (proposedStartDate <
            DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException(
                "Önerilen işe başlangıç tarihi geçmişte olamaz.",
                nameof(proposedStartDate));
        }

        if (offerExpiresAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Teklif son geçerlilik zamanı UTC olmalıdır.",
                nameof(offerExpiresAtUtc));
        }

        if (offerExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "Teklif son geçerlilik zamanı gelecekte olmalıdır.",
                nameof(offerExpiresAtUtc));
        }

        GrossSalary = grossSalary;
        Currency = normalizedCurrency;
        ProposedStartDate = proposedStartDate;
        OfferExpiresAtUtc = offerExpiresAtUtc;
        Status = HiringProcessStatus.OfferPrepared;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SendOffer(
        string offerResponseTokenHash)
    {
        if (Status != HiringProcessStatus.OfferPrepared)
        {
            throw new InvalidOperationException(
                "Yalnızca hazırlanmış bir teklif gönderilebilir.");
        }

        if (string.IsNullOrWhiteSpace(
                offerResponseTokenHash))
        {
            throw new ArgumentException(
                "Teklif cevap token hash değeri boş olamaz.",
                nameof(offerResponseTokenHash));
        }

        if (offerResponseTokenHash.Length != 64)
        {
            throw new ArgumentException(
                "Teklif cevap token hash değeri geçersiz.",
                nameof(offerResponseTokenHash));
        }

        Status = HiringProcessStatus.OfferSent;
        OfferResponseTokenHash =
            offerResponseTokenHash;
        OfferSentAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AcceptOffer(DateTime respondedAtUtc)
    {
        ValidateOfferResponse(
            respondedAtUtc,
            "kabul edilebilir");

        Status = HiringProcessStatus.OfferAccepted;
        OfferRespondedAtUtc = respondedAtUtc;
        OfferResponseTokenUsedAtUtc = respondedAtUtc;
        UpdatedAtUtc = respondedAtUtc;
    }

    public void RejectOffer(string reason)
    {
        RejectOffer(reason, DateTime.UtcNow);
    }

    public void RejectOffer(
        string reason,
        DateTime respondedAtUtc)
    {
        ValidateOfferResponse(
            respondedAtUtc,
            "reddedilebilir");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Teklif ret nedeni boş olamaz.",
                nameof(reason));
        }

        Status = HiringProcessStatus.OfferRejected;
        RejectionReason = reason.Trim();
        OfferRespondedAtUtc = respondedAtUtc;
        OfferResponseTokenUsedAtUtc = respondedAtUtc;
        UpdatedAtUtc = respondedAtUtc;
    }

    public void Cancel(string reason)
    {
        if (Status is HiringProcessStatus.Completed
            or HiringProcessStatus.OfferRejected
            or HiringProcessStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Tamamlanmış, reddedilmiş veya iptal edilmiş süreç iptal edilemez.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "İptal nedeni boş olamaz.",
                nameof(reason));
        }

        Status = HiringProcessStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Complete(Guid employeeId)
    {
        if (Status != HiringProcessStatus.OfferAccepted)
        {
            throw new InvalidOperationException(
                "Yalnızca teklifi kabul edilmiş işe alım süreci tamamlanabilir.");
        }

        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Çalışan kimliği boş olamaz.",
                nameof(employeeId));
        }

        EmployeeId = employeeId;
        Status = HiringProcessStatus.Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void ValidateOfferResponse(
        DateTime respondedAtUtc,
        string action)
    {
        if (Status != HiringProcessStatus.OfferSent)
        {
            throw new InvalidOperationException(
                $"Yalnızca gönderilmiş bir teklif {action}.");
        }

        if (respondedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Teklif cevap zamanı UTC olmalıdır.",
                nameof(respondedAtUtc));
        }

        if (OfferExpiresAtUtc is null ||
            OfferExpiresAtUtc <= respondedAtUtc)
        {
            throw new InvalidOperationException(
                "Süresi dolmuş bir teklife cevap verilemez.");
        }
    }
}
