using FluentValidation;
using MediatR;
using NexusHR.Contracts.Hiring;
using NexusHR.Hiring.Application.Abstractions.Messaging;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Application.Abstractions.Security;

namespace NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

internal sealed class RespondToOfferCommandHandler(
    IHiringProcessRepository hiringProcessRepository,
    IEligibleCandidateRepository eligibleCandidateRepository,
    IUnitOfWork unitOfWork,
    IOfferResponseTokenService tokenService,
    IIntegrationEventPublisher integrationEventPublisher,
    IValidator<RespondToOfferCommand> validator)
    : IRequestHandler<
        RespondToOfferCommand,
        RespondToOfferResponse>
{
    public async Task<RespondToOfferResponse> Handle(
        RespondToOfferCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult =
            await validator.ValidateAsync(
                request,
                cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(error => error.ErrorMessage)
                .ToArray();

            return RespondToOfferResponse.Failure(
                "OfferResponse.ValidationFailed",
                errors);
        }

        var tokenHash = tokenService.Hash(request.Token);

        var hiringProcess =
            await hiringProcessRepository
                .GetByOfferResponseTokenHashAsync(
                    tokenHash,
                    cancellationToken);

        if (hiringProcess is null)
        {
            return RespondToOfferResponse.Failure(
                "OfferResponse.InvalidToken",
                "Teklif bağlantısı geçersiz veya daha önce kullanılmış.");
        }

        var respondedAtUtc = DateTime.UtcNow;

        if (hiringProcess.OfferExpiresAtUtc is null ||
            hiringProcess.OfferExpiresAtUtc <= respondedAtUtc)
        {
            return RespondToOfferResponse.Failure(
                "OfferResponse.Expired",
                "Bu teklifin cevap süresi dolmuş.");
        }

        HiringOfferAcceptedIntegrationEvent? acceptedEvent = null;

        try
        {
            switch (request.Decision)
            {
                case OfferDecision.Accept:
                {
                    var eligibleCandidate =
                        await eligibleCandidateRepository
                            .GetByCandidateIdAsync(
                                hiringProcess.CandidateId,
                                cancellationToken);

                    if (eligibleCandidate is null)
                    {
                        return RespondToOfferResponse.Failure(
                            "OfferResponse.CandidateNotFound",
                            "Teklifi kabul eden aday bilgileri bulunamadı.");
                    }

                    hiringProcess.AcceptOffer(respondedAtUtc);

                    acceptedEvent =
                        new HiringOfferAcceptedIntegrationEvent(
                            EventId: Guid.NewGuid(),
                            HiringProcessId: hiringProcess.Id,
                            CandidateId: hiringProcess.CandidateId,
                            CandidateFirstName:
                                eligibleCandidate.FirstName,
                            CandidateLastName:
                                eligibleCandidate.LastName,
                            CandidateEmail:
                                eligibleCandidate.Email,
                            PositionTitle:
                                hiringProcess.PositionTitle,
                            Department:
                                hiringProcess.Department,
                            ProposedStartDate:
                                hiringProcess.ProposedStartDate!.Value,
                            OccurredAtUtc: respondedAtUtc);

                    break;
                }

                case OfferDecision.Reject:
                    hiringProcess.RejectOffer(
                        request.RejectionReason!,
                        respondedAtUtc);
                    break;

                default:
                    return RespondToOfferResponse.Failure(
                        "OfferResponse.InvalidDecision",
                        "Geçerli bir teklif kararı seçilmelidir.");
            }
        }
        catch (InvalidOperationException exception)
        {
            return RespondToOfferResponse.Failure(
                "OfferResponse.InvalidStatus",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return RespondToOfferResponse.Failure(
                "OfferResponse.InvalidRequest",
                exception.Message);
        }

        if (acceptedEvent is not null)
        {
            await integrationEventPublisher.PublishAsync(
                acceptedEvent,
                cancellationToken);
        }

        await hiringProcessRepository.UpdateAsync(
            hiringProcess,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return RespondToOfferResponse.Success();
    }
}
