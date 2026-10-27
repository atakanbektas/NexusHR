using MediatR;

namespace NexusHR.Hiring.Application.HiringProcesses.PrepareOffer;

public sealed record PrepareOfferCommand(
    Guid HiringProcessId,
    decimal GrossSalary,
    string Currency,
    DateOnly ProposedStartDate,
    DateTime OfferExpiresAtUtc)
    : IRequest<PrepareOfferResponse>;