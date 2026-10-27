using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Threading.RateLimiting;

namespace NexusHR.BuildingBlocks.Hosting;

public static class ApiDefaults
{
    public static WebApplicationBuilder AddApiDefaults(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsProduction() && builder.Configuration["Keycloak:Authority"] is { } authority &&
            (!authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             !builder.Configuration.GetValue<bool>("Keycloak:RequireHttpsMetadata") ||
             string.IsNullOrWhiteSpace(builder.Configuration["Keycloak:Audience"])))
            throw new InvalidOperationException("Production requires HTTPS identity metadata and an explicit API audience.");

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("public-offers", context => RateLimitPartition.GetSlidingWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new SlidingWindowRateLimiterOptions
                { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
        });
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.Configure<MvcOptions>(options => options.Filters.Add<ProblemDetailsFilter>());
        builder.Services.AddHealthChecks();
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
                {
                    // Public offer tokens must never become telemetry attributes.
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/api/public/offers");
                }).AddHttpClientInstrumentation().AddSource("MassTransit"))
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.WithTracing(tracing => tracing.AddOtlpExporter())
                .WithMetrics(metrics => metrics.AddOtlpExporter());
            builder.Logging.AddOpenTelemetry(options => options.AddOtlpExporter());
        }
        return builder;
    }

    public static void UseApiDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async status =>
        {
            await status.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                .TryWriteAsync(new ProblemDetailsContext { HttpContext = status.HttpContext,
                    ProblemDetails = new ProblemDetails { Status = status.HttpContext.Response.StatusCode } });
        });
        app.Use(async (context, next) =>
        {
            var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            using var scope = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request")
                .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
            await next(context);
        });
        app.UseRateLimiter();
        app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
        app.MapHealthChecks("/health/ready");
    }

    public static void AddDatabaseReadiness<TContext>(this IServiceCollection services) where TContext : DbContext =>
        services.AddHealthChecks().AddCheck<DatabaseReadiness<TContext>>("database", tags: ["ready"]);
}

internal sealed class DatabaseReadiness<TContext>(IServiceScopeFactory scopes) : IHealthCheck where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unavailable.");
    }
}

internal sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        var (status, code, detail) = exception switch
        {
            ValidationException => (400, "Validation.Failed", "One or more values are invalid."),
            KeyNotFoundException => (404, "Resource.NotFound", exception.Message),
            DbUpdateConcurrencyException => (409, "Concurrency.Conflict", "The record changed. Reload and retry."),
            DbUpdateException { InnerException: DbException { SqlState: "23505" } } =>
                (409, "Resource.AlreadyExists", "A record with these unique values already exists."),
            InvalidOperationException => (409, "Workflow.InvalidTransition", "The operation is not valid for the current state."),
            ArgumentException => (400, "Request.Invalid", "One or more values are invalid."),
            _ => (500, "Server.Error", "An unexpected error occurred.")
        };
        if (status == 500) logger.LogError(exception, "Unhandled request failure {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["errors"] = exception is ValidationException validation
            ? validation.Errors.Select(error => error.ErrorMessage).ToArray() : new[] { detail };
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}

internal sealed class ProblemDetailsFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { StatusCode: >= 400 } result || result.Value is ProblemDetails) return;
        var problem = new ProblemDetails { Status = result.StatusCode, Title = "Request failed" };
        // Preserve existing code/errors fields for current React clients while adopting ProblemDetails.
        var data = JsonSerializer.SerializeToElement(result.Value);
        if (data.ValueKind == JsonValueKind.Object)
            foreach (var field in data.EnumerateObject()) problem.Extensions[field.Name] = field.Value.Clone();
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        result.Value = problem;
        result.ContentTypes.Clear();
        result.ContentTypes.Add("application/problem+json");
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
