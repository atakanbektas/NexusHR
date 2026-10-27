namespace NexusHR.Onboarding.Application.EmployeeOnboardings
    .GetEmployeeOnboardings;

public sealed record EmployeeOnboardingListItemResponse(
    Guid Id,
    Guid HiringProcessId,
    Guid CandidateId,
    string FullName,
    string Email,
    string PositionTitle,
    string Department,
    DateOnly ProposedStartDate,
    string Status,
    int TotalTaskCount,
    int PendingTaskCount,
    DateTime CreatedAtUtc);
