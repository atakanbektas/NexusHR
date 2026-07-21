using FluentValidation;
using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Candidates.UploadCandidateCv;

internal sealed class UploadCandidateCvCommandHandler(
    ICandidateRepository candidateRepository,
    ICandidateDocumentRepository documentRepository,
    ICandidateDocumentStorage documentStorage,
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

        await documentStorage.UploadAsync(
            objectName,
            request.FileContent,
            request.ContentType,
            cancellationToken);

        try
        {
            await documentRepository.AddAsync(
                document,
                cancellationToken);
        }
        catch
        {
            await documentStorage.DeleteAsync(
                objectName,
                CancellationToken.None);

            throw;
        }

        return UploadCandidateCvResponse.Success(document.Id);
    }
}