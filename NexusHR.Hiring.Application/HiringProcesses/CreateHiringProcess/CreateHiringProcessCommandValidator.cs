using FluentValidation;

namespace NexusHR.Hiring.Application.HiringProcesses.CreateHiringProcess;

public sealed class CreateHiringProcessCommandValidator
    : AbstractValidator<CreateHiringProcessCommand>
{
    public CreateHiringProcessCommandValidator()
    {
        RuleFor(x => x.CandidateId)
            .NotEmpty()
            .WithMessage("Aday kimliği boş olamaz.");

        RuleFor(x => x.PositionTitle)
            .NotEmpty()
            .WithMessage("Pozisyon adı boş olamaz.")
            .MaximumLength(150)
            .WithMessage("Pozisyon adı en fazla 150 karakter olabilir.");

        RuleFor(x => x.Department)
            .NotEmpty()
            .WithMessage("Departman adı boş olamaz.")
            .MaximumLength(150)
            .WithMessage("Departman adı en fazla 150 karakter olabilir.");

        RuleFor(x => x.EmploymentType)
            .IsInEnum()
            .WithMessage("Geçerli bir istihdam türü seçilmelidir.");
    }
}