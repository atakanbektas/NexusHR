namespace NexusHR.Hiring.Api
    .Contracts.PublicOffers;

public sealed record RespondToOfferRequest(
    string Token,
    string Decision,
    string? RejectionReason);