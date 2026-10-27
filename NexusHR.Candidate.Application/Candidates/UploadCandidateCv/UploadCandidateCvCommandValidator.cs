using FluentValidation;

namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

public sealed class UploadCandidateCvCommandValidator
    : AbstractValidator<UploadCandidateCvCommand>
{
    private const int MaximumFileSize = 5 * 1024 * 1024;

    public UploadCandidateCvCommandValidator()
    {
        RuleFor(command => command.CandidateId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Aday kimliği boş olamaz.");

        RuleFor(command => command.FileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Dosya adı boş olamaz.")
            .Must(fileName =>
                string.Equals(Path.GetExtension(fileName),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            .WithMessage("Yalnızca PDF dosyaları yüklenebilir.").MaximumLength(255);

        RuleFor(command => command.ContentType)
            .Equal(
                "application/pdf",
                StringComparer.OrdinalIgnoreCase)
            .WithMessage("Dosyanın içerik türü PDF olmalıdır.");

        RuleFor(command => command.FileContent)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("CV dosyası boş olamaz.")
            .Must(content => content.Length <= MaximumFileSize)
            .WithMessage("CV dosyası en fazla 5 MB olabilir.")
            .Must(IsPdf)
            .WithMessage("Yüklenen dosya geçerli bir PDF değil.");
    }

    private static bool IsPdf(byte[] content)
    {
        return content.Length >= 5 &&
               content[0] == '%' &&
               content[1] == 'P' &&
               content[2] == 'D' &&
               content[3] == 'F' &&
               content[4] == '-';
    }
}
