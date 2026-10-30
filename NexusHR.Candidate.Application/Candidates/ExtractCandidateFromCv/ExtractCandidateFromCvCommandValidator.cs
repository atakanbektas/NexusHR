using FluentValidation;
using NexusHR.Candidate.Application.CvAnalyses;
namespace NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;
public sealed class ExtractCandidateFromCvCommandValidator : AbstractValidator<ExtractCandidateFromCvCommand>
{
    public ExtractCandidateFromCvCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FileContent).Cascade(CascadeMode.Stop).NotEmpty().Must(x => x.Length <= 5 * 1024 * 1024);
        RuleFor(x => x).Must(x => CvFileSignature.Matches(x.FileName, CvFileSignature.ContentType(x.FileContent), x.FileContent))
            .WithMessage("PDF, JPG, PNG veya WEBP dosyası seçin. İçerik imzası uzantıyla eşleşmelidir.");
    }
}
