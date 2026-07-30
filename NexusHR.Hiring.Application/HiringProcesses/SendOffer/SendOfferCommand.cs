using MediatR;

namespace NexusHR.Hiring.Application.HiringProcesses.SendOffer;

public sealed record SendOfferCommand(
    Guid HiringProcessId)
    : IRequest<SendOfferResponse>;