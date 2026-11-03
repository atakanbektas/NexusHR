using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NexusHR.Hiring.Api
    .Contracts.PublicOffers;
using NexusHR.Hiring.Application
    .HiringProcesses.RespondToOffer;

namespace NexusHR.Hiring.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/offers")]
[EnableRateLimiting("public-offers")]
[RequestSizeLimit(16 * 1024)]
public sealed class PublicOffersController(
    ISender sender)
    : ControllerBase
{
    private static readonly System.Diagnostics.ActivitySource Telemetry = new("NexusHR.Business");
    [HttpPost("respond")]
    public async Task<IActionResult> Respond(
        RespondToOfferRequest request,
        CancellationToken cancellationToken)
    {
        // The capability-bearing HTTP operation remains excluded from tracing. Begin a
        // clean business root with no incoming baggage, token, identity or request URL.
        var previous = System.Diagnostics.Activity.Current;
        System.Diagnostics.Activity.Current = null;
        try {
            using var activity = Telemetry.StartActivity("Offer decision");
            activity?.SetTag("nexushr.operation", "offer-decision");
            if (activity is not null && ControllerContext.HttpContext is { } http) http.Response.Headers["X-Correlation-ID"] = activity.TraceId.ToString();
            var result = await RespondCore(request, cancellationToken);
            if(result is Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult {StatusCode: >=400}) activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            return result;
        } finally { System.Diagnostics.Activity.Current = previous; }
    }
    private async Task<IActionResult> RespondCore(RespondToOfferRequest request, CancellationToken cancellationToken)
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
