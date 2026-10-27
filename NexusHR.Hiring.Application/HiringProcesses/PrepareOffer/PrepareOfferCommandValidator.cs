using FluentValidation;

namespace NexusHR.Hiring.Application.HiringProcesses.PrepareOffer;

public sealed class PrepareOfferCommandValidator
    : AbstractValidator<PrepareOfferCommand>
{
    public PrepareOfferCommandValidator()
    {
        RuleFor(x => x.HiringProcessId)
            .NotEmpty()
            .WithMessage("İşe alım süreci kimliği boş olamaz.");

        RuleFor(x => x.GrossSalary)
            .GreaterThan(0)
            .WithMessage("Brüt maaş sıfırdan büyük olmalıdır.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Para birimi boş olamaz.")
            .Length(3)
            .WithMessage(
                "Para birimi üç karakterli ISO kodu olmalıdır.");

        RuleFor(x => x.ProposedStartDate)
            .Must(date =>
                date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(
                "Önerilen işe başlangıç tarihi geçmişte olamaz.");

        RuleFor(x => x.OfferExpiresAtUtc)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage(
                "Teklif son geçerlilik zamanı UTC olmalıdır.")
            .Must(date => date > DateTime.UtcNow)
            .WithMessage(
                "Teklif son geçerlilik zamanı gelecekte olmalıdır.");
    }
}