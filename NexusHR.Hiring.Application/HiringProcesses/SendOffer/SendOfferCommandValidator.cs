using FluentValidation;

namespace NexusHR.Hiring.Application.HiringProcesses.SendOffer;

public sealed class SendOfferCommandValidator
    : AbstractValidator<SendOfferCommand>
{
    public SendOfferCommandValidator()
    {
        RuleFor(x => x.HiringProcessId)
            .NotEmpty()
            .WithMessage(
                "İşe alım süreci kimliği boş olamaz.");
    }
}