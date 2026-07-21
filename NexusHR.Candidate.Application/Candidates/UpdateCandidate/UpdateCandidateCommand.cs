using MediatR;

namespace NexusHR.Candidate.Application.Candidates.UpdateCandidate;

public sealed record UpdateCandidateCommand(
    Guid CandidateId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber)
    : IRequest<UpdateCandidateResponse>;