using FluentValidation;

namespace NexusHR.Candidate.Application.Candidates.UpdateCandidate;

public sealed class UpdateCandidateCommandValidator
    : AbstractValidator<UpdateCandidateCommand>
{
    public UpdateCandidateCommandValidator()
    {
        RuleFor(x => x.CandidateId)
            .NotEmpty()
            .WithMessage("Aday kimliği boş olamaz.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Aday adı boş olamaz.")
            .MaximumLength(100)
            .WithMessage("Aday adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Aday soyadı boş olamaz.")
            .MaximumLength(100)
            .WithMessage("Aday soyadı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("E-posta adresi boş olamaz.")
            .EmailAddress()
            .WithMessage("Geçerli bir e-posta adresi girilmelidir.")
            .MaximumLength(250)
            .WithMessage("E-posta adresi en fazla 250 karakter olabilir.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Telefon numarası boş olamaz.")
            .MaximumLength(30)
            .WithMessage("Telefon numarası en fazla 30 karakter olabilir.");
    }
}