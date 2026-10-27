using FluentValidation;
using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

internal sealed class UploadCandidateCvCommandHandler(
    ICandidateRepository candidateRepository,
    ICandidateDocumentRepository documentRepository,
    ICandidateCvUpload cvUpload,
    IValidator<UploadCandidateCvCommand> validator)
    : IRequestHandler<
        UploadCandidateCvCommand,
        UploadCandidateCvResponse>
{
    public async Task<UploadCandidateCvResponse> Handle(
        UploadCandidateCvCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(error => error.ErrorMessage)
                .ToArray();

            return UploadCandidateCvResponse.Failure(
                "Candidate.CvValidationFailed",
                errors);
        }

        var candidate = await candidateRepository.GetByIdAsync(
            request.CandidateId,
            cancellationToken);

        if (candidate is null)
        {
            return UploadCandidateCvResponse.Failure(
                "Candidate.NotFound",
                "CV yüklenecek aday bulunamadı.");
        }

        var cvExists = await documentRepository.ExistsAsync(
            candidate.Id,
            CandidateDocumentType.Cv,
            cancellationToken);

        if (cvExists)
        {
            return UploadCandidateCvResponse.Failure(
                "Candidate.CvAlreadyExists",
                "Aday için daha önce CV yüklenmiş.");
        }

        if (candidate.Status != CandidateStatus.Draft)
            return UploadCandidateCvResponse.Failure("Candidate.InvalidStatus", "CV upload requires a draft candidate.");

        var documentId = Guid.NewGuid();

        var objectName =
            $"candidates/{candidate.Id:N}/cv/{documentId:N}.pdf";

        var document = new CandidateDocument(
            documentId,
            candidate.Id,
            CandidateDocumentType.Cv,
            request.FileName,
            objectName,
            request.ContentType,
            request.FileContent.LongLength);

        await cvUpload.SaveAsync(candidate, document, request.FileContent, cancellationToken);

        return UploadCandidateCvResponse.Success(document.Id);
    }
}
