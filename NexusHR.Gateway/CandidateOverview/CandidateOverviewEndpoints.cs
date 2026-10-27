using System.Net;

namespace NexusHR.Gateway.CandidateOverview;

internal static class CandidateOverviewEndpoints
{
    public static IEndpointRouteBuilder MapCandidateOverviewEndpoints(
        this IEndpointRouteBuilder endpoints,
        string authorizationPolicy)
    {
        endpoints.MapGet(
                "/api/candidate-overview",
                GetCandidateOverviewAsync)
            .RequireAuthorization(authorizationPolicy);

        return endpoints;
    }

    private static async Task<IResult> GetCandidateOverviewAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        HttpContext httpContext,
        CandidateOverviewService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await service.GetAsync(
                page,
                pageSize,
                search,
                status,
                httpContext.Request.Headers.Authorization
                    .FirstOrDefault(),
                cancellationToken);

            return Results.Ok(response);
        }
        catch (CandidateOverviewValidationException exception)
        {
            return Results.BadRequest(new
            {
                code = "CandidateOverview.ValidationFailed",
                errors = new[]
                {
                    exception.Message
                }
            });
        }
        catch (CandidateOverviewDownstreamException exception)
        {
            var statusCode = exception.StatusCode is
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden
                    ? (int)exception.StatusCode
                    : StatusCodes.Status502BadGateway;

            return Results.Json(
                new
                {
                    code = "CandidateOverview.DownstreamFailure",
                    errors = new[]
                    {
                        "Aday ve işe alım bilgileri birleştirilemedi."
                    }
                },
                statusCode: statusCode);
        }
    }
}
