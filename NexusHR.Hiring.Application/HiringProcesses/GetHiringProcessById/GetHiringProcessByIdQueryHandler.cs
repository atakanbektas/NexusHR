using MediatR;
using NexusHR.Hiring.Application.Abstractions.Persistence;

namespace NexusHR.Hiring.Application.HiringProcesses.GetHiringProcessById;

internal sealed class GetHiringProcessByIdQueryHandler(
    IHiringProcessRepository hiringProcessRepository,
    IEligibleCandidateRepository eligibleCandidateRepository)
    : IRequestHandler<
        GetHiringProcessByIdQuery,
        HiringProcessDetailResponse?>
{
    public async Task<HiringProcessDetailResponse?> Handle(
        GetHiringProcessByIdQuery request,
        CancellationToken cancellationToken)
    {
        var hiringProcess =
            await hiringProcessRepository.GetByIdAsync(
                request.HiringProcessId,
                cancellationToken);

        if (hiringProcess is null)
        {
            return null;
        }

        var candidate =
            await eligibleCandidateRepository.GetByCandidateIdAsync(
                hiringProcess.CandidateId,
                cancellationToken);

        var candidateFullName = candidate is null
            ? "Bilinmeyen Aday"
            : $"{candidate.FirstName} {candidate.LastName}";

        var candidateEmail =
            candidate?.Email ?? string.Empty;

        return new HiringProcessDetailResponse(
            hiringProcess.Id,
            hiringProcess.CandidateId,
            candidateFullName,
            candidateEmail,
            hiringProcess.EmployeeId,
            hiringProcess.PositionTitle,
            hiringProcess.Department,
            hiringProcess.EmploymentType.ToString(),
            hiringProcess.Status.ToString(),
            hiringProcess.GrossSalary,
            hiringProcess.Currency,
            hiringProcess.ProposedStartDate,
            hiringProcess.OfferExpiresAtUtc,
            hiringProcess.OfferSentAtUtc,
            hiringProcess.OfferRespondedAtUtc,
            hiringProcess.RejectionReason,
            hiringProcess.CancellationReason,
            hiringProcess.CreatedAtUtc,
            hiringProcess.UpdatedAtUtc);
    }
}