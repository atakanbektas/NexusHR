using FluentValidation;

namespace NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

public sealed class RespondToOfferCommandValidator
    : AbstractValidator<RespondToOfferCommand>
{
    public RespondToOfferCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .WithMessage(
                "Teklif cevap token değeri boş olamaz.")
            .MaximumLength(256)
            .WithMessage(
                "Teklif cevap token değeri geçersiz.");

        RuleFor(command => command.Decision)
            .IsInEnum()
            .WithMessage(
                "Geçerli bir teklif kararı seçilmelidir.");

        When(
            command =>
                command.Decision ==
                OfferDecision.Reject,
            () =>
            {
                RuleFor(command =>
                        command.RejectionReason)
                    .NotEmpty()
                    .WithMessage(
                        "Teklif reddedilirken ret nedeni girilmelidir.")
                    .MaximumLength(1000)
                    .WithMessage(
                        "Ret nedeni en fazla 1000 karakter olabilir.");
            });
    }
}