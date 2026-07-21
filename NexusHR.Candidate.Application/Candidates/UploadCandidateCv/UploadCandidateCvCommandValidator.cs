using FluentValidation;

namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

public sealed class UploadCandidateCvCommandValidator
    : AbstractValidator<UploadCandidateCvCommand>
{
    private const int MaximumFileSize = 5 * 1024 * 1024;

    public UploadCandidateCvCommandValidator()
    {
        RuleFor(command => command.CandidateId)
            .NotEmpty()
            .WithMessage("Aday kimliği boş olamaz.");

        RuleFor(command => command.FileName)
            .NotEmpty()
            .WithMessage("Dosya adı boş olamaz.")
            .Must(fileName =>
                Path.GetExtension(fileName).Equals(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            .WithMessage("Yalnızca PDF dosyaları yüklenebilir.");

        RuleFor(command => command.ContentType)
            .Equal(
                "application/pdf",
                StringComparer.OrdinalIgnoreCase)
            .WithMessage("Dosyanın içerik türü PDF olmalıdır.");

        RuleFor(command => command.FileContent)
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