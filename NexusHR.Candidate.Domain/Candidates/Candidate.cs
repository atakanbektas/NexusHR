namespace NexusHR.Candidate.Domain.Candidates;

public sealed class Candidate
{
    private Candidate()
    {
    }

    public Candidate(
        Guid id,
        string firstName,
        string lastName,
        string email,
        string phoneNumber)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Aday kimliği boş olamaz.",
                nameof(id));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException(
                "Aday adı boş olamaz.",
                nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException(
                "Aday soyadı boş olamaz.",
                nameof(lastName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "E-posta adresi boş olamaz.",
                nameof(email));

        Id = id;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber.Trim();
        Status = CandidateStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string PhoneNumber { get; private set; } = null!;

    public CandidateStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public void UpdateContactInformation(
        string email,
        string phoneNumber)
    {
        if (Status == CandidateStatus.Archived)
            throw new InvalidOperationException(
                "Arşivlenmiş aday güncellenemez.");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "E-posta adresi boş olamaz.",
                nameof(email));

        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkDocumentsPending()
    {
        if (Status != CandidateStatus.Draft)
            throw new InvalidOperationException(
                "Yalnızca taslak aday belge bekliyor durumuna alınabilir.");

        Status = CandidateStatus.DocumentsPending;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkReadyForHiring()
    {
        if (Status != CandidateStatus.DocumentsPending)
            throw new InvalidOperationException(
                "Aday belge bekliyor durumunda olmalıdır.");

        Status = CandidateStatus.ReadyForHiring;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == CandidateStatus.Archived)
            return;

        Status = CandidateStatus.Archived;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}