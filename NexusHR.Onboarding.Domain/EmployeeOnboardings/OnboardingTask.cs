namespace NexusHR.Onboarding.Domain.EmployeeOnboardings;

public sealed class OnboardingTask
{
    private OnboardingTask()
    {
    }

    internal OnboardingTask(
        Guid id,
        Guid employeeOnboardingId,
        string title,
        OnboardingTaskType type,
        string assignedDepartment,
        DateOnly dueDate,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Görev kimliği boş olamaz.",
                nameof(id));
        }

        if (employeeOnboardingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Onboarding kimliği boş olamaz.",
                nameof(employeeOnboardingId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Görev başlığı boş olamaz.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(assignedDepartment))
        {
            throw new ArgumentException(
                "Görevin atanacağı birim boş olamaz.",
                nameof(assignedDepartment));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Görev oluşturulma zamanı UTC olmalıdır.",
                nameof(createdAtUtc));
        }

        Id = id;
        EmployeeOnboardingId = employeeOnboardingId;
        Title = title.Trim();
        Type = type;
        AssignedDepartment = assignedDepartment.Trim();
        Status = OnboardingTaskStatus.Pending;
        DueDate = dueDate;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid EmployeeOnboardingId { get; private set; }

    public string Title { get; private set; } = null!;

    public OnboardingTaskType Type { get; private set; }

    public string AssignedDepartment { get; private set; } = null!;

    public OnboardingTaskStatus Status { get; private set; }

    public DateOnly DueDate { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }
}
