using FluentValidation;
using NexusHR.Candidate.Application.CvAnalyses;

namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

public sealed class UploadCandidateCvCommandValidator : AbstractValidator<UploadCandidateCvCommand>
{
    public UploadCandidateCvCommandValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FileContent).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(x => x.Length <= 5 * 1024 * 1024).WithMessage("CV en fazla 5 MB olabilir.");
        RuleFor(x => x).Must(x => CvFileSignature.Matches(x.FileName, x.ContentType, x.FileContent))
            .WithMessage("Dosya imzası, uzantısı ve içerik türü eşleşmelidir. PDF, JPG, PNG veya WEBP kullanın.");
    }
}
