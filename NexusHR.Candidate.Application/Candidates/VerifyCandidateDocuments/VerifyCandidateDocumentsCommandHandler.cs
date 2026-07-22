using MediatR;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Domain.Candidates;

namespace NexusHR.Candidate.Application.Candidates.VerifyCandidateDocuments;

internal sealed class VerifyCandidateDocumentsCommandHandler(
    ICandidateRepository candidateRepository,
    ICandidateDocumentRepository documentRepository)
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

        await candidateRepository.UpdateAsync(
            candidate,
            cancellationToken);

        return VerifyCandidateDocumentsResponse.Success();
    }
}