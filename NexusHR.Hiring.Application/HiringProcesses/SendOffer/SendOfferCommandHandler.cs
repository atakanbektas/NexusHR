using FluentValidation;
using MediatR;
using NexusHR.Contracts.Hiring;
using NexusHR.Hiring.Application.Abstractions.Messaging;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using NexusHR.Hiring.Application.Abstractions.Security;

namespace NexusHR.Hiring.Application.HiringProcesses.SendOffer;

internal sealed class SendOfferCommandHandler(
    IHiringProcessRepository hiringProcessRepository,
    IEligibleCandidateRepository eligibleCandidateRepository,
    IUnitOfWork unitOfWork,
    IOfferResponseTokenService tokenService,
    IIntegrationEventPublisher integrationEventPublisher,
    IValidator<SendOfferCommand> validator)
    : IRequestHandler<
        SendOfferCommand,
        SendOfferResponse>
{
    public async Task<SendOfferResponse> Handle(
        SendOfferCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult =
            await validator.ValidateAsync(
                request,
                cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(
                    error => error.ErrorMessage)
                .ToArray();

            return SendOfferResponse.Failure(
                "HiringProcess.ValidationFailed",
                errors);
        }

        var hiringProcess =
            await hiringProcessRepository
                .GetByIdAsync(
                    request.HiringProcessId,
                    cancellationToken);

        if (hiringProcess is null)
        {
            return SendOfferResponse.Failure(
                "HiringProcess.NotFound",
                "İşe alım süreci bulunamadı.");
        }

        var eligibleCandidate =
            await eligibleCandidateRepository
                .GetByCandidateIdAsync(
                    hiringProcess.CandidateId,
                    cancellationToken);

        if (eligibleCandidate is null)
        {
            return SendOfferResponse.Failure(
                "HiringProcess.CandidateNotFound",
                "Teklif gönderilecek aday bulunamadı.");
        }

        var generatedToken =
            tokenService.Generate();

        try
        {
            hiringProcess.SendOffer(
                generatedToken.Hash);
        }
        catch (
            InvalidOperationException exception)
        {
            return SendOfferResponse.Failure(
                "HiringProcess.InvalidStatus",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return SendOfferResponse.Failure(
                "HiringProcess.InvalidToken",
                exception.Message);
        }

        var integrationEvent =
            new HiringOfferSentIntegrationEvent(
                EventId: Guid.NewGuid(),
                HiringProcessId:
                    hiringProcess.Id,
                CandidateId:
                    hiringProcess.CandidateId,
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
                GrossSalary:
                    hiringProcess.GrossSalary!.Value,
                Currency:
                    hiringProcess.Currency!,
                ProposedStartDate:
                    hiringProcess
                        .ProposedStartDate!.Value,
                OfferExpiresAtUtc:
                    hiringProcess
                        .OfferExpiresAtUtc!.Value,
                ResponseToken:
                    generatedToken.PlainText,
                OccurredAtUtc:
                    hiringProcess
                        .OfferSentAtUtc!.Value);

        await integrationEventPublisher
            .PublishAsync(
                integrationEvent,
                cancellationToken);

        await hiringProcessRepository.UpdateAsync(
            hiringProcess,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return SendOfferResponse.Success();
    }
}