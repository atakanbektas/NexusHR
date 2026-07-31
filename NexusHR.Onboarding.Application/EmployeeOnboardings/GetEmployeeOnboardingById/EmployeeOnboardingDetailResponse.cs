namespace NexusHR.Onboarding.Application.EmployeeOnboardings
    .GetEmployeeOnboardingById;

public sealed record EmployeeOnboardingDetailResponse(
    Guid Id,
    Guid HiringProcessId,
    Guid CandidateId,
    string FirstName,
    string LastName,
    string Email,
    string PositionTitle,
    string Department,
    DateOnly ProposedStartDate,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<OnboardingTaskResponse> Tasks);

public sealed record OnboardingTaskResponse(
    Guid Id,
    string Title,
    string Type,
    string AssignedDepartment,
    string Status,
    DateOnly DueDate,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
