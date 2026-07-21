using FluentValidation;

namespace NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;

public sealed class ExtractCandidateFromCvCommandValidator
    : AbstractValidator<ExtractCandidateFromCvCommand>
{
    private const int MaximumFileSize = 5 * 1024 * 1024;

    public ExtractCandidateFromCvCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("CV dosyasının adı boş olamaz.")
            .Must(fileName =>
                Path.GetExtension(fileName)
                    .Equals(
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase))
            .WithMessage("Yalnızca PDF dosyaları yüklenebilir.");

        RuleFor(x => x.FileContent)
            .NotEmpty()
            .WithMessage("CV dosyası boş olamaz.")
            .Must(content => content.Length <= MaximumFileSize)
            .WithMessage("CV dosyası en fazla 5 MB olabilir.");
    }
}