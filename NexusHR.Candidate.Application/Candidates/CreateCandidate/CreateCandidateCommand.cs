using MediatR;

namespace NexusHR.Candidate.Application.Candidates.CreateCandidate;

public sealed record CreateCandidateCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber)
    : IRequest<CreateCandidateResponse>;