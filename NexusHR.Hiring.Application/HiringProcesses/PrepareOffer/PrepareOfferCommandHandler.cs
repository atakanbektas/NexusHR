using FluentValidation;
using MediatR;
using NexusHR.Hiring.Application.Abstractions.Persistence;

namespace NexusHR.Hiring.Application.HiringProcesses.PrepareOffer;

internal sealed class PrepareOfferCommandHandler(
    IHiringProcessRepository hiringProcessRepository,
    IUnitOfWork unitOfWork,
    IValidator<PrepareOfferCommand> validator)
    : IRequestHandler<
        PrepareOfferCommand,
        PrepareOfferResponse>
{
    public async Task<PrepareOfferResponse> Handle(
        PrepareOfferCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(error => error.ErrorMessage)
                .ToArray();

            return PrepareOfferResponse.Failure(
                "HiringProcess.OfferValidationFailed",
                errors);
        }

        var hiringProcess =
            await hiringProcessRepository.GetByIdAsync(
                request.HiringProcessId,
                cancellationToken);

        if (hiringProcess is null)
        {
            return PrepareOfferResponse.Failure(
                "HiringProcess.NotFound",
                "İşe alım süreci bulunamadı.");
        }

        try
        {
            hiringProcess.PrepareOffer(
                request.GrossSalary,
                request.Currency,
                request.ProposedStartDate,
                request.OfferExpiresAtUtc);
        }
        catch (InvalidOperationException exception)
        {
            return PrepareOfferResponse.Failure(
                "HiringProcess.InvalidStatus",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return PrepareOfferResponse.Failure(
                "HiringProcess.OfferValidationFailed",
                exception.Message);
        }

        await hiringProcessRepository.UpdateAsync(
            hiringProcess,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return PrepareOfferResponse.Success();
    }
}