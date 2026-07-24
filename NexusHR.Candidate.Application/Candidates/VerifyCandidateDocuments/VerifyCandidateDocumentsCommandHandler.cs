using MediatR;
using NexusHR.Candidate.Application.Abstractions.Messaging;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Contracts.Candidates;

namespace NexusHR.Candidate.Application.Candidates.VerifyCandidateDocuments;

internal sealed class VerifyCandidateDocumentsCommandHandler(
    ICandidateRepository candidateRepository,
    ICandidateDocumentRepository documentRepository,
    IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<
        VerifyCandidateDocumentsCommand,
        VerifyCandidateDocumentsResponse>
{
    public async Task<VerifyCandidateDocumentsResponse> Handle(
        VerifyCandidateDocumentsCommand request,
        CancellationToken cancellationToken)
    {
        var candidate = await candidateRepository.GetByIdAsync(
            request.CandidateId,
            cancellationToken);

        if (candidate is null)
        {
            return VerifyCandidateDocumentsResponse.Failure(
                "Candidate.NotFound",
                "Belgeleri doğrulanacak aday bulunamadı.");
        }

        var cvExists = await documentRepository.ExistsAsync(
            candidate.Id,
            CandidateDocumentType.Cv,
            cancellationToken);

        if (!cvExists)
        {
            return VerifyCandidateDocumentsResponse.Failure(
                "Candidate.CvMissing",
                "Adayın belgeleri doğrulanmadan önce CV yüklenmelidir.");
        }

        if (candidate.Status != CandidateStatus.DocumentsPending)
        {
            return VerifyCandidateDocumentsResponse.Failure(
                "Candidate.InvalidStatus",
                "Yalnızca belge kontrolü bekleyen adaylar doğrulanabilir.");
        }

        candidate.MarkReadyForHiring();

        var occurredAtUtc = DateTime.UtcNow;

        var integrationEvent =
            new CandidateReadyForHiringIntegrationEvent(
                EventId: Guid.NewGuid(),
                CandidateId: candidate.Id,
                FirstName: candidate.FirstName,
                LastName: candidate.LastName,
                Email: candidate.Email,
                OccurredAtUtc: occurredAtUtc);

        await integrationEventPublisher.PublishAsync(
            integrationEvent,
            cancellationToken);

        await candidateRepository.UpdateAsync(
            candidate,
            cancellationToken);

        return VerifyCandidateDocumentsResponse.Success();
    }
}