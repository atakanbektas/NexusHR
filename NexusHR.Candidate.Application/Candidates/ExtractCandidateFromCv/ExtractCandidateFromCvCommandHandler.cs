using FluentValidation;
using MediatR;
using NexusHR.Candidate.Application.Abstractions.Documents;

namespace NexusHR.Candidate.Application.Candidates.ExtractCandidateFromCv;

internal sealed class ExtractCandidateFromCvCommandHandler(
    ICvInformationExtractor cvInformationExtractor,
    IValidator<ExtractCandidateFromCvCommand> validator)
    : IRequestHandler<
        ExtractCandidateFromCvCommand,
        ExtractCandidateFromCvResponse>
{
    public async Task<ExtractCandidateFromCvResponse> Handle(
        ExtractCandidateFromCvCommand request,
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

            return ExtractCandidateFromCvResponse.Failure(
                "Candidate.CvValidationFailed",
                errors);
        }

        try
        {
            var draft = await cvInformationExtractor.ExtractAsync(
                request.FileContent,
                cancellationToken);

            var informationFound =
                !string.IsNullOrWhiteSpace(draft.FirstName) ||
                !string.IsNullOrWhiteSpace(draft.LastName) ||
                !string.IsNullOrWhiteSpace(draft.Email) ||
                !string.IsNullOrWhiteSpace(draft.PhoneNumber);

            if (!informationFound)
            {
                return ExtractCandidateFromCvResponse.Failure(
                    "Candidate.CvInformationNotFound",
                    "CV içerisinden aday bilgisi çıkarılamadı.");
            }

            return ExtractCandidateFromCvResponse.Success(draft);
        }
        catch (InvalidDataException exception)
        {
            return ExtractCandidateFromCvResponse.Failure(
                "Candidate.InvalidCvFile",
                exception.Message);
        }
    }
}