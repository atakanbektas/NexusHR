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
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            var response = await service.GetAsync(
                page,
                pageSize,
                search,
                status,
                httpContext.Request.Headers.Authorization
                    .FirstOrDefault(),
                deadline.Token);

            return Results.Ok(response);
        }
        catch (CandidateOverviewValidationException exception)
        {
            return Problem(httpContext, 400, "CandidateOverview.ValidationFailed", exception.Message);
        }
        catch (CandidateOverviewDownstreamException exception)
        {
            var statusCode = exception.StatusCode is
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden
                    ? (int)exception.StatusCode
                    : StatusCodes.Status502BadGateway;

            return Problem(httpContext, statusCode, "CandidateOverview.DownstreamFailure", "Aday ve işe alım bilgileri birleştirilemedi.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Problem(httpContext, 504, "CandidateOverview.Timeout", "Bilgiler zamanında alınamadı. Yeniden deneyin."); }
        catch (HttpRequestException)
        { return Problem(httpContext, 502, "CandidateOverview.DownstreamFailure", "Aday ve işe alım bilgileri birleştirilemedi."); }
    }

    private static IResult Problem(HttpContext context, int status, string code, string message) => Results.Problem(
        statusCode: status, title: code, extensions: new Dictionary<string, object?> {
            ["code"] = code, ["errors"] = new[] { message }, ["traceId"] = context.TraceIdentifier });
}
