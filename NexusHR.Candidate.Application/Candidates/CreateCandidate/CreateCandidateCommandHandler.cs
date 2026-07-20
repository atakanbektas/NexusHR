using FluentValidation;
using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using CandidateEntity =
    NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.Application.Candidates.CreateCandidate;

internal sealed class CreateCandidateCommandHandler(
    ICandidateRepository candidateRepository,
    IValidator<CreateCandidateCommand> validator)
    : IRequestHandler<CreateCandidateCommand, CreateCandidateResponse>
{
    public async Task<CreateCandidateResponse> Handle(
        CreateCandidateCommand request,
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

            return CreateCandidateResponse.Failure(
                "Candidate.ValidationFailed",
                errors);
        }

        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

        var emailExists = await candidateRepository.EmailExistsAsync(
            normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return CreateCandidateResponse.Failure(
                "Candidate.EmailAlreadyExists",
                "Bu e-posta adresiyle kayıtlı bir aday bulunmaktadır.");
        }

        var candidate = new CandidateEntity(
            Guid.NewGuid(),
            request.FirstName,
            request.LastName,
            normalizedEmail,
            request.PhoneNumber);

        await candidateRepository.AddAsync(
            candidate,
            cancellationToken);

        return CreateCandidateResponse.Success(candidate.Id);
    }
}