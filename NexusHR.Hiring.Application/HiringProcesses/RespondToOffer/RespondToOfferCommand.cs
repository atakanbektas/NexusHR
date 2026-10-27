using MediatR;

namespace NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

public sealed record RespondToOfferCommand(
    string Token,
    OfferDecision Decision,
    string? RejectionReason)
    : IRequest<RespondToOfferResponse>;