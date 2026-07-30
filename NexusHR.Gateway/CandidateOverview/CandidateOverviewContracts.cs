namespace NexusHR.Gateway.CandidateOverview;

internal sealed record CandidateOverviewResponse(
    IReadOnlyCollection<CandidateOverviewItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

internal sealed record CandidateOverviewItemResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string CandidateStatus,
    Guid? HiringProcessId,
    string? HiringStatus,
    string DisplayStatusSource,
    string DisplayStatus,
    DateTime CreatedAtUtc);

internal sealed record CandidatePageResponse(
    IReadOnlyCollection<CandidateItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

internal sealed record CandidateItemResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Status,
    DateTime CreatedAtUtc);

internal sealed record CandidateHiringProcessStatusResponse(
    Guid CandidateId,
    Guid HiringProcessId,
    string Status);

internal sealed record CandidateIdsRequest(
    IReadOnlyCollection<Guid> CandidateIds);
