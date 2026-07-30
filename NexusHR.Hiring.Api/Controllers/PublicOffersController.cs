using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusHR.Hiring.Api
    .Contracts.PublicOffers;
using NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

namespace NexusHR.Hiring.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/offers")]
public sealed class PublicOffersController(
    ISender sender)
    : ControllerBase
{
    [HttpPost("respond")]
    public async Task<IActionResult> Respond(
        RespondToOfferRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseDecision(
                request.Decision,
                out var decision))
        {
            return BadRequest(new
            {
                code =
                    "OfferResponse.InvalidDecision",
                errors = new[]
                {
                    "Karar değeri Accept veya Reject olmalıdır."
                }
            });
        }

        var command =
            new RespondToOfferCommand(
                request.Token,
                decision,
                request.RejectionReason);

        var response =
            await sender.Send(
                command,
                cancellationToken);

        if (response.IsSuccess)
        {
            return NoContent();
        }

        var errorResponse = new
        {
            code = response.ErrorCode,
            errors = response.Errors
        };

        return response.ErrorCode switch
        {
            "OfferResponse.Expired" =>
                StatusCode(
                    StatusCodes.Status410Gone,
                    errorResponse),

            "OfferResponse.InvalidStatus" =>
                Conflict(errorResponse),

            _ => BadRequest(errorResponse)
        };
    }

    private static bool TryParseDecision(
        string? decisionValue,
        out OfferDecision decision)
    {
        switch (
            decisionValue?
                .Trim()
                .ToLowerInvariant())
        {
            case "accept":
                decision =
                    OfferDecision.Accept;
                return true;

            case "reject":
                decision =
                    OfferDecision.Reject;
                return true;

            default:
                decision = default;
                return false;
        }
    }
}