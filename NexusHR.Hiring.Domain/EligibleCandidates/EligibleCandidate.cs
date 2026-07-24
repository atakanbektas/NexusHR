namespace NexusHR.Hiring.Domain.EligibleCandidates;

public sealed class EligibleCandidate
{
    private EligibleCandidate()
    {
    }

    public EligibleCandidate(
        Guid candidateId,
        string firstName,
        string lastName,
        string email,
        DateTime becameEligibleAtUtc)
    {
        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException(
                "Aday kimliği boş olamaz.",
                nameof(candidateId));
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException(
                "Aday adı boş olamaz.",
                nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException(
                "Aday soyadı boş olamaz.",
                nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Aday e-posta adresi boş olamaz.",
                nameof(email));
        }

        if (becameEligibleAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Aday uygunluk zamanı UTC olmalıdır.",
                nameof(becameEligibleAtUtc));
        }

        CandidateId = candidateId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        BecameEligibleAtUtc = becameEligibleAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid CandidateId { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public DateTime BecameEligibleAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}