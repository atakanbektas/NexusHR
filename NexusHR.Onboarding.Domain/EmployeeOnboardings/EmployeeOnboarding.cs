namespace NexusHR.Onboarding.Domain.EmployeeOnboardings;

public sealed class EmployeeOnboarding
{
    private readonly List<OnboardingTask> _tasks = [];

    private EmployeeOnboarding()
    {
    }

    public EmployeeOnboarding(
        Guid id,
        Guid hiringProcessId,
        Guid candidateId,
        string firstName,
        string lastName,
        string email,
        string positionTitle,
        string department,
        DateOnly proposedStartDate,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Onboarding kimliği boş olamaz.",
                nameof(id));
        }

        if (hiringProcessId == Guid.Empty)
        {
            throw new ArgumentException(
                "İşe alım süreci kimliği boş olamaz.",
                nameof(hiringProcessId));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException(
                "Aday kimliği boş olamaz.",
                nameof(candidateId));
        }

        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException(
                "Çalışan adı ve soyadı boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Çalışan e-posta adresi boş olamaz.",
                nameof(email));
        }

        if (string.IsNullOrWhiteSpace(positionTitle) ||
            string.IsNullOrWhiteSpace(department))
        {
            throw new ArgumentException(
                "Pozisyon ve departman bilgisi boş olamaz.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Onboarding oluşturulma zamanı UTC olmalıdır.",
                nameof(createdAtUtc));
        }

        Id = id;
        HiringProcessId = hiringProcessId;
        CandidateId = candidateId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        PositionTitle = positionTitle.Trim();
        Department = department.Trim();
        ProposedStartDate = proposedStartDate;
        Status = EmployeeOnboardingStatus.Draft;
        CreatedAtUtc = createdAtUtc;

        CreateDefaultItTasks(createdAtUtc);
    }

    public Guid Id { get; private set; }

    public Guid HiringProcessId { get; private set; }

    public Guid CandidateId { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string PositionTitle { get; private set; } = null!;

    public string Department { get; private set; } = null!;

    public DateOnly ProposedStartDate { get; private set; }

    public EmployeeOnboardingStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OnboardingTask> Tasks =>
        _tasks.AsReadOnly();

    private void CreateDefaultItTasks(DateTime createdAtUtc)
    {
        var dueDate = ProposedStartDate.AddDays(-1);

        _tasks.Add(
            new OnboardingTask(
                Guid.NewGuid(),
                Id,
                "Bilgisayar ve çevre birimlerini hazırla",
                OnboardingTaskType.EquipmentPreparation,
                "IT",
                dueDate,
                createdAtUtc));

        _tasks.Add(
            new OnboardingTask(
                Guid.NewGuid(),
                Id,
                "Kurumsal kullanıcı hesabını oluştur",
                OnboardingTaskType.CorporateAccountProvisioning,
                "IT",
                dueDate,
                createdAtUtc));

        _tasks.Add(
            new OnboardingTask(
                Guid.NewGuid(),
                Id,
                "Pozisyona uygun erişim yetkilerini tanımla",
                OnboardingTaskType.AccessProvisioning,
                "IT",
                dueDate,
                createdAtUtc));
    }
}
