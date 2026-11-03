using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Threading.RateLimiting;

namespace NexusHR.Gateway.Engineering;

public static class EngineeringModule
{
    public const string Reader = "EngineeringReader";
    public const string Operator = "EngineeringOperator";
    public static void AddEngineering(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("Engineering").Get<EngineeringOptions>() ?? new();
        builder.Services.AddSingleton(options);
        if (!options.Enabled) return;
        if (new NpgsqlConnectionStringBuilder(options.Database).Database != "nexushr_engineering")
            throw new InvalidOperationException("Engineering requires its own nexushr_engineering database.");
        if (options.ExecutionEnabled && (!builder.Environment.IsDevelopment() || options.AgentKey.Length < 32))
            throw new InvalidOperationException("Execution is Development-only and needs a separate strong agent key.");
        if (options.InitializeDatabase && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("Initialize the engineering schema through a controlled migration outside Development.");
        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")) {
            if(new[]{options.Prometheus,options.Tempo,options.Loki,options.Broker}.Any(v=>!Uri.TryCreate(v,UriKind.Absolute,out var u)||u.Scheme!="https"||u.UserInfo.Length>0))
                throw new InvalidOperationException("Non-development engineering sources require explicit HTTPS endpoints.");
            if(new[]{options.Database}.Concat(options.Databases.Values).Any(v=>new NpgsqlConnectionStringBuilder(v).SslMode!=SslMode.VerifyFull))
                throw new InvalidOperationException("Non-development engineering database connections require certificate-verified TLS.");
        }
        if (options.MaxVus is < 1 or > 10000) throw new InvalidOperationException("Engineering MaxVus must be 1..10000.");
        builder.Services.AddSingleton<EngineeringStore>();
        builder.Services.AddSingleton<EngineeringDiagnostics>();
        builder.Services.AddHttpClient("engineering", client => client.Timeout = TimeSpan.FromSeconds(8))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, EngineeringAgentAuthentication>("EngineeringAgent", _ => { });
        builder.Services.AddAuthorization(o => {
            o.AddPolicy(Reader, p => p.RequireAuthenticatedUser().RequireClaim("sub").RequireRole(Reader, Operator));
            o.AddPolicy(Operator, p => p.RequireAuthenticatedUser().RequireClaim("sub").RequireRole(Operator));
            o.AddPolicy("EngineeringAgent", p => p.AddAuthenticationSchemes("EngineeringAgent").RequireAuthenticatedUser().RequireRole("EngineeringAgent"));
        });
        builder.Services.AddRateLimiter(o => o.AddPolicy("engineering-write", c => RateLimitPartition.GetFixedWindowLimiter(
            c.User.FindFirstValue("sub") ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    }
    public static async Task MapEngineeringAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<EngineeringOptions>();
        if (!options.Enabled) return;
        app.Use(async (context, next) => {
            if (context.Request.Path.StartsWithSegments("/api/engineering") || context.Request.Path.StartsWithSegments("/api/engineering-agent")) {
                context.Response.Headers.CacheControl="no-store";
                var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 1_000_000;
            }
            await next(context);
        });
        if (options.InitializeDatabase) await app.Services.GetRequiredService<EngineeringStore>().InitializeAsync(app.Lifetime.ApplicationStopping);
        var read = app.MapGroup("/api/engineering").RequireAuthorization(Reader);
        read.MapGet("/capabilities", async (EngineeringStore store, CancellationToken ct) => {
            var seen = await store.AgentSeenAsync(ct);
            return new { executionEnabled = options.ExecutionEnabled, maxVus = options.MaxVus,
            agentSeenAtUtc = seen, agentOnline = seen.HasValue && seen > DateTime.UtcNow.AddSeconds(-45),
            environment = "synthetic-lab", presets = new[] { 100, 500, 1000, 2500, 5000, 10000 },
            maxDurationSeconds = 120, maxQueuedRuns = 5, nativeTokenRateAvailable = true,
            modelBenchmarksAvailable = options.ExecutionEnabled, benchmarkModels=new[]{"qwen3:4b","qwen2.5vl:3b"}, messageReplayAvailable = false }; });
        read.MapGet("/overview", (EngineeringDiagnostics d, CancellationToken ct) => d.OverviewAsync(ct));
        read.MapGet("/infrastructure", (EngineeringStore store, CancellationToken ct) => store.InventoryAsync(ct));
        read.MapGet("/metrics/{metric}", (string metric, EngineeringDiagnostics d, CancellationToken ct) => d.MetricAsync(metric, ct));
        read.MapGet("/databases/{service}", (string service, EngineeringDiagnostics d, CancellationToken ct) => d.DatabaseAsync(service, ct));
        read.MapGet("/broker", (EngineeringDiagnostics d, CancellationToken ct) => d.BrokerAsync(ct));
        read.MapGet("/traces", (EngineeringDiagnostics d, CancellationToken ct) => d.TracesAsync(ct));
        read.MapGet("/traces/{id}", (string id, EngineeringDiagnostics d, CancellationToken ct) => d.TraceAsync(id, ct));
        read.MapGet("/logs", (string? service, string? severity, string? traceId, int? minutes, EngineeringDiagnostics d, CancellationToken ct) => d.LogsAsync(service, severity, traceId, minutes, ct));
        read.MapGet("/ci", (EngineeringDiagnostics d, CancellationToken ct) => d.CiAsync(ct));
        read.MapGet("/ai", (HttpContext h, EngineeringDiagnostics d, CancellationToken ct) => d.AiAsync(h.Request.Headers.Authorization.ToString(), ct));
        read.MapGet("/runs", (EngineeringStore store, CancellationToken ct) => store.ListAsync(ct));
        read.MapGet("/runs/{id:guid}", async (Guid id, EngineeringStore store, CancellationToken ct) => await store.GetAsync(id, ct) is { } run ? Results.Ok(run) : Results.NotFound());
        read.MapGet("/audit", (EngineeringStore store, CancellationToken ct) => store.AuditAsync(ct));
        read.MapGet("/runs/{id:guid}/export", async (Guid id, string? format, EngineeringStore store, CancellationToken ct) => {
            var run = await store.GetAsync(id, ct);
            if (run is null) return Results.NotFound();
            if (format == "csv") {
                var update = run.Report?.Deserialize<RunUpdate>(EngineeringStore.Json);
                if(update?.Benchmarks is { } benchmarks) {
                    var text=new StringBuilder("model,dataset,status,durationSeconds,validatedFacts,goldMarkersMatched,goldMarkersTotal,nativeTokensPerSecond,peakCpuPercent,peakMemoryBytes\n");
                    foreach(var b in benchmarks)text.AppendLine(FormattableString.Invariant($"{b.Model},{b.Dataset},{b.Status},{b.DurationSeconds},{b.ValidatedFacts},{b.GoldMarkersMatched},{b.GoldMarkersTotal},{b.NativeOutputTokensPerSecond},{b.PeakCpuPercent},{b.PeakMemoryBytes}"));
                    return Results.File(Encoding.UTF8.GetBytes(text.ToString()),"text/csv",$"engineering-{id}.csv");
                }
                var csv = new StringBuilder("elapsedSeconds,requests,failures,timeouts,rps,averageMs,p50Ms,p95Ms,p99Ms,cpuPercent,memoryBytes,databaseConnections,queueDepth\n");
                foreach (var s in update?.Samples ?? []) csv.AppendLine(FormattableString.Invariant($"{s.ElapsedSeconds},{s.Requests},{s.Failures},{s.Timeouts},{s.Rps},{s.AverageMs},{s.P50Ms},{s.P95Ms},{s.P99Ms},{s.CpuPercent},{s.MemoryBytes},{s.DatabaseConnections},{s.QueueDepth}"));
                return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"engineering-{id}.csv");
            }
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(run, EngineeringStore.Json), "application/json", $"engineering-{id}.json");
        });
        read.MapPost("/runs", async (ExperimentRequest request, HttpContext h, EngineeringStore store, CancellationToken ct) => {
            if (!options.ExecutionEnabled) return Results.Problem(statusCode: 409, title: "Engineering.ExecutionDisabled");
            if (request.Validate(options.MaxVus) is { } error) return Results.Problem(statusCode: 400, title: "Engineering.InvalidExperiment", detail: error);
            if (!Guid.TryParse(h.Request.Headers["Idempotency-Key"], out var key)) return Results.Problem(statusCode: 400, title: "Engineering.IdempotencyKeyRequired");
            var id = await store.CreateAsync(h.User.FindFirstValue("sub")!, key, request, ct);
            return Results.Accepted($"/api/engineering/runs/{id}", new { id });
        }).RequireAuthorization(Operator).RequireRateLimiting("engineering-write");
        read.MapPost("/runs/{id:guid}/cancel", async (Guid id, HttpContext h, EngineeringStore s, CancellationToken ct) =>
            await s.CancelAsync(id, h.User.FindFirstValue("sub")!, ct) ? Results.Accepted() : Results.Conflict())
            .RequireAuthorization(Operator).RequireRateLimiting("engineering-write");
        var agent = app.MapGroup("/api/engineering-agent").RequireAuthorization("EngineeringAgent");
        agent.MapPost("/inventory", async (InventoryUpdate data, EngineeringStore store, CancellationToken ct) => {
            if (!options.ExecutionEnabled || !data.IsValid()) return Results.BadRequest();
            await store.SaveInventoryAsync(data,ct);return Results.Ok(new { stored=true });
        });
        agent.MapPost("/claim", async (EngineeringStore store, CancellationToken ct) => !options.ExecutionEnabled ? Results.Conflict() : Results.Ok(new { claim = await store.ClaimAsync(ct) }));
        agent.MapPost("/runs/{id:guid}/{lease:guid}/heartbeat", async(Guid id,Guid lease,EngineeringStore store,CancellationToken ct)=>
            await store.RenewAsync(id,lease,ct) is { } cancelled ? Results.Ok(new {cancellationRequested=cancelled}) : Results.Conflict());
        agent.MapPost("/runs/{id:guid}/{lease:guid}", async (Guid id, Guid lease, RunUpdate report, EngineeringStore store, CancellationToken ct) => {
            if (!options.ExecutionEnabled || !report.IsValid()) return Results.BadRequest();
            var cancel = await store.ReportAsync(id, lease, report, ct);
            return cancel is null ? Results.Conflict() : Results.Ok(new { cancellationRequested = cancel.Value });
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1_000_000));
    }
}

internal sealed class EngineeringAgentAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e,
    EngineeringOptions config) : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var candidate = Request.Headers["X-Engineering-Agent-Key"].ToString();
        if (!config.ExecutionEnabled || candidate.Length != config.AgentKey.Length || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(config.AgentKey)))
            return Task.FromResult(AuthenticateResult.Fail("Invalid engineering agent credential."));
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "engineering-agent"), new(ClaimTypes.Role, "EngineeringAgent")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
