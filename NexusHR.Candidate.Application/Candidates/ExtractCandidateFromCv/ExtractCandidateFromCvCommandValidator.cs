using FluentValidation;

namespace NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;

public sealed class ExtractCandidateFromCvCommandValidator
    : AbstractValidator<ExtractCandidateFromCvCommand>
{
    private const int MaximumFileSize = 5 * 1024 * 1024;

    public ExtractCandidateFromCvCommandValidator()
    {
        RuleFor(x => x.FileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("CV dosyasının adı boş olamaz.")
            .Must(fileName =>
                string.Equals(Path.GetExtension(fileName),
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase))
            .WithMessage("Yalnızca PDF dosyaları yüklenebilir.").MaximumLength(255);

        RuleFor(x => x.FileContent)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("CV dosyası boş olamaz.")
            .Must(content => content.Length <= MaximumFileSize)
            .WithMessage("CV dosyası en fazla 5 MB olabilir.");
    }
}
