using FluentValidation;
using MediatR;
using NexusHR.Hiring.Application.Abstractions.Persistence;
using HiringProcessEntity =
    NexusHR.Hiring.Domain.HiringProcesses.HiringProcess;

namespace NexusHR.Hiring.Application.HiringProcesses.CreateHiringProcess;

internal sealed class CreateHiringProcessCommandHandler(
    IEligibleCandidateRepository eligibleCandidateRepository,
    IHiringProcessRepository hiringProcessRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateHiringProcessCommand> validator)
    : IRequestHandler<
        CreateHiringProcessCommand,
        CreateHiringProcessResponse>
{
    public async Task<CreateHiringProcessResponse> Handle(
        CreateHiringProcessCommand request,
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

            return CreateHiringProcessResponse.Failure(
                "HiringProcess.ValidationFailed",
                errors);
        }

        var eligibleCandidate =
            await eligibleCandidateRepository.GetByCandidateIdAsync(
                request.CandidateId,
                cancellationToken);

        if (eligibleCandidate is null)
        {
            return CreateHiringProcessResponse.Failure(
                "HiringProcess.CandidateNotEligible",
                "Aday işe alım süreci başlatmaya uygun değil.");
        }

        var hasActiveProcess =
            await hiringProcessRepository.HasActiveProcessAsync(
                request.CandidateId,
                cancellationToken);

        if (hasActiveProcess)
        {
            return CreateHiringProcessResponse.Failure(
                "HiringProcess.ActiveProcessAlreadyExists",
                "Aday için devam eden bir işe alım süreci bulunmaktadır.");
        }

        var hiringProcess = new HiringProcessEntity(
            Guid.NewGuid(),
            eligibleCandidate.CandidateId,
            request.PositionTitle,
            request.Department,
            request.EmploymentType);

        await hiringProcessRepository.AddAsync(
            hiringProcess,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return CreateHiringProcessResponse.Success(
            hiringProcess.Id);
    }
}