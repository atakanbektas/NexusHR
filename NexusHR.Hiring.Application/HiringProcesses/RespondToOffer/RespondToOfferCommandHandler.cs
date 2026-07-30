using FluentValidation;
using MediatR;
using NexusHR.Hiring.Application
    .Abstractions.Persistence;
using NexusHR.Hiring.Application
    .Abstractions.Security;

namespace NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

internal sealed class RespondToOfferCommandHandler(
    IHiringProcessRepository hiringProcessRepository,
    IUnitOfWork unitOfWork,
    IOfferResponseTokenService tokenService,
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

        var tokenHash =
            tokenService.Hash(
                request.Token);

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

        var respondedAtUtc =
            DateTime.UtcNow;

        if (hiringProcess.OfferExpiresAtUtc is null ||
            hiringProcess.OfferExpiresAtUtc <=
            respondedAtUtc)
        {
            return RespondToOfferResponse.Failure(
                "OfferResponse.Expired",
                "Bu teklifin cevap süresi dolmuş.");
        }

        try
        {
            switch (request.Decision)
            {
                case OfferDecision.Accept:
                    hiringProcess.AcceptOffer(
                        respondedAtUtc);
                    break;

                case OfferDecision.Reject:
                    hiringProcess.RejectOffer(
                        request.RejectionReason!);
                    break;

                default:
                    return RespondToOfferResponse
                        .Failure(
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

        await hiringProcessRepository.UpdateAsync(
            hiringProcess,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return RespondToOfferResponse.Success();
    }
}