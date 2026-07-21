using FluentValidation;
using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Candidates.UpdateCandidate;

internal sealed class UpdateCandidateCommandHandler(
    ICandidateRepository candidateRepository,
    IValidator<UpdateCandidateCommand> validator)
    : IRequestHandler<UpdateCandidateCommand, UpdateCandidateResponse>
{
    public async Task<UpdateCandidateResponse> Handle(
        UpdateCandidateCommand request,
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

            return UpdateCandidateResponse.Failure(
                "Candidate.ValidationFailed",
                errors);
        }

        var candidate = await candidateRepository.GetByIdAsync(
            request.CandidateId,
            cancellationToken);

        if (candidate is null)
        {
            return UpdateCandidateResponse.Failure(
                "Candidate.NotFound",
                "Güncellenecek aday bulunamadı.");
        }

        if (candidate.Status == CandidateStatus.Archived)
        {
            return UpdateCandidateResponse.Failure(
                "Candidate.Archived",
                "Arşivlenmiş aday güncellenemez.");
        }

        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

        var emailExists = await candidateRepository.EmailExistsAsync(
            normalizedEmail,
            candidate.Id,
            cancellationToken);

        if (emailExists)
        {
            return UpdateCandidateResponse.Failure(
                "Candidate.EmailAlreadyExists",
                "Bu e-posta adresiyle kayıtlı başka bir aday bulunmaktadır.");
        }

        candidate.UpdateInformation(
            request.FirstName,
            request.LastName,
            normalizedEmail,
            request.PhoneNumber);

        await candidateRepository.UpdateAsync(
            candidate,
            cancellationToken);

        return UpdateCandidateResponse.Success();
    }
}