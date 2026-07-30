namespace NexusHR.Hiring.Application.HiringProcesses.GetHiringProcessById;

public sealed record HiringProcessDetailResponse(
    Guid Id,
    Guid CandidateId,
    string CandidateFullName,
    string CandidateEmail,
    Guid? EmployeeId,
    string PositionTitle,
    string Department,
    string EmploymentType,
    string Status,
    decimal? GrossSalary,
    string? Currency,
    DateOnly? ProposedStartDate,
    DateTime? OfferExpiresAtUtc,
    DateTime? OfferSentAtUtc,
    DateTime? OfferRespondedAtUtc,
    string? RejectionReason,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);