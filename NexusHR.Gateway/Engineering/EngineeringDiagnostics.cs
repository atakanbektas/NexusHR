using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace NexusHR.Gateway.Engineering;

public sealed class EngineeringDiagnostics(IHttpClientFactory clients, EngineeringOptions options, IConfiguration config)
{
    private readonly SemaphoreSlim ciGate = new(1);
    private SourceResult? cachedCi;
    private DateTime ciExpires;
    public static readonly string[] Services = ["gateway", "candidate", "hiring", "onboarding", "notification"];
    internal static readonly Dictionary<string, string> Queries = new()
    {
        ["cpu"] = "sum by(container_label_com_docker_compose_service)(rate(container_cpu_usage_seconds_total{container_label_com_docker_compose_project=\"nexushr\",image!=\"\"}[1m])) * 100",
        ["memory"] = "sum by(container_label_com_docker_compose_service)(container_memory_working_set_bytes{container_label_com_docker_compose_project=\"nexushr\",image!=\"\"})",
        ["network"] = "sum by(container_label_com_docker_compose_service)(rate(container_network_receive_bytes_total{container_label_com_docker_compose_project=\"nexushr\",image!=\"\"}[1m]))",
        ["restarts"] = "changes(container_start_time_seconds{container_label_com_docker_compose_project=\"nexushr\",image!=\"\"}[15m])",
        ["host-memory"] = "node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes",
        ["disk"] = "node_filesystem_size_bytes{fstype!~\"tmpfs|overlay|proc|sysfs\"} - node_filesystem_avail_bytes{fstype!~\"tmpfs|overlay|proc|sysfs\"}",
        ["requests"] = "sum by(service_name)(rate(http_server_request_duration_seconds_count[1m]))",
        ["errors"] = "sum by(service_name)(rate(http_server_request_duration_seconds_count{http_response_status_code=~\"5..\"}[1m]))",
        ["latency"] = "histogram_quantile(0.95, sum by(le,service_name)(rate(http_server_request_duration_seconds_bucket[1m]))) * 1000",
        ["pool"] = "sum by(service_name,db_client_connection_state)(db_client_connection_count)",
        ["ai-duration"] = "histogram_quantile(0.95,sum by(le)(rate(nexushr_ai_attempt_duration_seconds_bucket[5m])))",
        ["ai-attempts"] = "sum by(outcome,provider)(nexushr_ai_attempts_total)",
        ["ai-evidence"] = "sum by(disposition)(nexushr_ai_evidence_total)",
        ["ai-tokens"] = "sum by(direction)(nexushr_ai_tokens_total)",
    };
    private HttpClient Client => clients.CreateClient("engineering");
    private static object? String(JsonElement e, string name) => e.TryGetProperty(name, out var p) ? p.GetString() : null;
    private static string? Identifier(JsonElement element,string name)
    {
        var text=String(element,name)?.ToString();if(string.IsNullOrEmpty(text))return null;
        if(Regex.IsMatch(text,"^[a-f0-9]{16}([a-f0-9]{16})?$"))return text;
        try {var bytes=Convert.FromBase64String(text);return bytes.Length is 8 or 16?Convert.ToHexStringLower(bytes):null;}catch(FormatException){return null;}
    }
    private async Task<JsonElement> FetchAsync(string root, string path, CancellationToken ct, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, root.TrimEnd('/') + path); configure?.Invoke(request);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream(); var buffer = new byte[8192];
        while (true) { var n = await stream.ReadAsync(buffer, ct); if (n == 0) break; if (output.Length + n > 4_000_000) throw new InvalidDataException(); output.Write(buffer, 0, n); }
        return JsonSerializer.Deserialize<JsonElement>(output.ToArray());
    }
    private static async Task<SourceResult> Safe(Func<Task<object>> action)
    {
        try { return new(true, await action()); }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException or InvalidDataException or NpgsqlException or InvalidOperationException or KeyNotFoundException)
        { return new(false, null, "Source unavailable or incompatible; no values were fabricated."); }
    }
    private string ServiceUrl(string service) => (service switch
    {
        "gateway" => "http://localhost:8080", "notification" => "http://notification-api:8080",
        "candidate" or "hiring" or "onboarding" => config[$"ReverseProxy:Clusters:{service}-cluster:Destinations:{service}-api:Address"] ?? $"http://{service}-api:8080",
        _ => throw new ArgumentException("Service is not allowlisted.")
    }).TrimEnd('/');
    public async Task<object> OverviewAsync(CancellationToken ct)
    {
        var services = await Task.WhenAll(Services.Select(async service => {
            var started = System.Diagnostics.Stopwatch.StartNew();
            try { using var r = await Client.GetAsync(ServiceUrl(service) + "/health/ready", ct); return new { service, healthy = r.IsSuccessStatusCode, latencyMs = started.Elapsed.TotalMilliseconds }; }
            catch (HttpRequestException) { return new { service, healthy = false, latencyMs = started.Elapsed.TotalMilliseconds }; }
            catch (OperationCanceledException) { return new { service, healthy = false, latencyMs = started.Elapsed.TotalMilliseconds }; }
        }));
        return new { observedAtUtc = DateTime.UtcNow, services, scope = "Single-employer metadata only; no CV access", metrics = Queries.Keys };
    }
    public Task<SourceResult> MetricAsync(string metric, CancellationToken ct)
    {
        if (!Queries.TryGetValue(metric, out var query)) throw new ArgumentException("Metric is not allowlisted.");
        return Safe(async () => {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var result = await FetchAsync(options.Prometheus, $"/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={now - 900}&end={now}&step=15", ct);
            return result.GetProperty("data").GetProperty("result").EnumerateArray().Take(40).Select(series => new {
                labels = series.GetProperty("metric").EnumerateObject().Where(p => p.Name is "service_name" or "container_label_com_docker_compose_service" or "state" or "db_client_connection_state" or "outcome" or "provider" or "disposition" or "direction").ToDictionary(p => p.Name, p => p.Value.GetString()),
                values = series.GetProperty("values").EnumerateArray().Select(v => new double?[] { v[0].GetDouble(), double.TryParse(v[1].GetString(), System.Globalization.CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null }).ToArray()
            }).ToArray();
        });
    }
    public Task<SourceResult> DatabaseAsync(string service, CancellationToken ct)
    {
        if (!Services.Contains(service) || !options.Databases.TryGetValue(service, out var connection)) return Task.FromResult(new SourceResult(false, null, "Read-only diagnostics connection not configured."));
        return Safe(async () => {
            var builder = new NpgsqlConnectionStringBuilder(connection) { Options = "-c default_transaction_read_only=on -c statement_timeout=3000", MaxPoolSize = 2, Timeout = 3, CommandTimeout = 3 };
            await using var c = new NpgsqlConnection(builder.ConnectionString); await c.OpenAsync(ct);
            await using var summary = new NpgsqlCommand("""
                SELECT numbackends, xact_commit, xact_rollback, deadlocks, pg_database_size(datname),
                (SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock')
                FROM pg_stat_database WHERE datname=current_database()
                """, c);
            object stats;
            await using (var r = await summary.ExecuteReaderAsync(ct)) { if (!await r.ReadAsync(ct)) throw new InvalidDataException(); stats = new { connections = r.GetInt32(0), commitsTotal = r.GetInt64(1), rollbacksTotal = r.GetInt64(2), deadlocks = r.GetInt64(3), sizeBytes = r.GetInt64(4), lockWaits = r.GetInt64(5) }; }
            var indexes = new List<object>();
            await using (var cmd = new NpgsqlCommand("SELECT relname,indexrelname,idx_scan FROM pg_stat_user_indexes ORDER BY idx_scan DESC LIMIT 30", c))
            await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) indexes.Add(new { table = r.GetString(0), index = r.GetString(1), scans = r.GetInt64(2) });
            var statements = new List<object>(); var statementsAvailable = true;
            try {
                await using var cmd = new NpgsqlCommand("SELECT queryid::text,calls,mean_exec_time,total_exec_time,rows FROM pg_stat_statements WHERE dbid=(SELECT oid FROM pg_database WHERE datname=current_database()) ORDER BY total_exec_time DESC LIMIT 20", c);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) statements.Add(new { fingerprint = r.IsDBNull(0) ? "restricted" : r.GetString(0), calls = r.GetInt64(1), meanMs = r.GetDouble(2), totalMs = r.GetDouble(3), rows = r.GetInt64(4) });
            } catch (PostgresException) { statementsAvailable = false; }
            return new { service, observedAtUtc=DateTime.UtcNow, stats, indexes, statementsAvailable, statements, executionPlansAvailable = false,
                planLimit = "Arbitrary SQL/EXPLAIN and query text are intentionally unavailable; use an approved offline diagnostic session." };
        });
    }
    public Task<SourceResult> BrokerAsync(CancellationToken ct) => Safe(async () => {
        void Auth(HttpRequestMessage r) => r.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.BrokerUser}:{options.BrokerPassword}")));
        var queues = await FetchAsync(options.Broker, "/api/queues/%2F?page=1&page_size=100&pagination=true&disable_stats=false", ct, Auth);
        var exchanges = await FetchAsync(options.Broker, "/api/exchanges/%2F", ct, Auth);
        var data = queues.GetProperty("items").EnumerateArray().Select(q => new {
            name = String(q, "name"), consumers = q.TryGetProperty("consumers", out var consumers) ? consumers.GetInt32() : 0,
            ready = q.TryGetProperty("messages_ready", out var ready) ? ready.GetInt64() : 0,
            unacknowledged = q.TryGetProperty("messages_unacknowledged", out var unack) ? unack.GetInt64() : 0,
            errorQueue = q.GetProperty("name").GetString()!.EndsWith("_error"), skippedQueue = q.GetProperty("name").GetString()!.EndsWith("_skipped"),
            publishRate = Rate(q, "publish_details"), deliveryRate = Rate(q, "deliver_get_details") }).ToArray();
        var stores=await Task.WhenAll(new[]{"candidate","hiring","onboarding"}.Select(async service=>new {service,source=await MessageStoreAsync(service,ct)}));
        return new { queues = data, messageStores=stores, exchanges = exchanges.EnumerateArray().Take(100).Select(e => new { name = String(e, "name"), type = String(e, "type") }).ToArray(),
            truncated = queues.TryGetProperty("total_count", out var count) && count.GetInt32() > 100, replaySupported = false,
            retryNote = "MassTransit bounded retries are verified by integration tests; queue rates alone do not prove retry success." };
    });
    private Task<SourceResult> MessageStoreAsync(string service,CancellationToken ct) => Safe(async()=>{
        if(!options.Databases.TryGetValue(service,out var value))throw new InvalidOperationException();
        var builder=new NpgsqlConnectionStringBuilder(value){Options="-c default_transaction_read_only=on -c statement_timeout=3000",CommandTimeout=3,Timeout=3,MaxPoolSize=2};
        await using var c=new NpgsqlConnection(builder.ConnectionString);await c.OpenAsync(ct);
        await using var command=new NpgsqlCommand("SELECT outbox_rows,outbox_state_rows,inbox_rows FROM engineering_message_counts",c);
        await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))throw new InvalidDataException();
        return new {outboxRows=reader.GetInt64(0),outboxStateRows=reader.GetInt64(1),inboxRows=reader.GetInt64(2)};
    });
    private static double? Rate(JsonElement q, string property) => q.TryGetProperty("message_stats", out var stats) && stats.TryGetProperty(property, out var details) && details.TryGetProperty("rate", out var rate) ? rate.GetDouble() : null;
    public Task<SourceResult> TracesAsync(CancellationToken ct) => Safe(async () => {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = await FetchAsync(options.Tempo, $"/api/search?limit=30&start={now - 900}&end={now}", ct);
        return result.GetProperty("traces").EnumerateArray().Select(t => new { traceId = String(t, "traceID"), service = String(t, "rootServiceName"), durationMs = t.TryGetProperty("durationMs", out var ms) ? ms.GetDouble() : 0 }).ToArray();
    });
    public Task<SourceResult> TraceAsync(string id, CancellationToken ct)
    {
        if (!Regex.IsMatch(id, "^[a-f0-9]{32}$")) throw new ArgumentException("Invalid trace ID.");
        return Safe(async () => {
            var json = await FetchAsync(options.Tempo, $"/api/traces/{id}", ct, r => r.Headers.Accept.ParseAdd("application/json"));
            var batches = json.TryGetProperty("batches", out var b) ? b : json.GetProperty("resourceSpans");
            var spans = new List<object>();
            foreach (var batch in batches.EnumerateArray()) {
                var attributes = batch.GetProperty("resource").GetProperty("attributes");
                var service = attributes.EnumerateArray().FirstOrDefault(a => a.GetProperty("key").GetString() == "service.name");
                var scopes = batch.TryGetProperty("scopeSpans", out var sc) ? sc : batch.GetProperty("instrumentationLibrarySpans");
                foreach (var scope in scopes.EnumerateArray()) foreach (var s in scope.GetProperty("spans").EnumerateArray()) {
                    var start = long.Parse(s.GetProperty("startTimeUnixNano").ToString(), System.Globalization.CultureInfo.InvariantCulture);
                    var end = long.Parse(s.GetProperty("endTimeUnixNano").ToString(), System.Globalization.CultureInfo.InvariantCulture);
                    spans.Add(new { id = Identifier(s, "spanId"), parentId = Identifier(s, "parentSpanId"), name = String(s, "name"),
                        service = service.ValueKind == JsonValueKind.Undefined ? "unknown" : service.GetProperty("value").GetProperty("stringValue").GetString(),
                        startUnixMs = start / 1_000_000d, durationMs = (end - start) / 1_000_000d,
                        error = s.TryGetProperty("status", out var status) && status.TryGetProperty("code", out var code) && code.ToString() is "2" or "STATUS_CODE_ERROR" });
                }
            }
            return new { traceId = id, correlationId = id, spans, criticalPathNote = "Parent relationships and measured durations; not a causal critical-path proof for concurrent asynchronous work." };
        });
    }
    public Task<SourceResult> LogsAsync(string? service, string? severity, string? traceId, int? minutes, CancellationToken ct)
    {
        if (service is not null && !Services.Contains(service)) throw new ArgumentException("Service is not allowlisted.");
        if (severity is not null && severity is not ("INFO" or "WARN" or "ERROR" or "DEBUG")) throw new ArgumentException("Severity is not allowlisted.");
        if (traceId is not null && !Regex.IsMatch(traceId, "^[a-f0-9]{32}$")) throw new ArgumentException("Invalid correlation ID.");
        if(minutes is <1 or >60)throw new ArgumentException("Log window must be 1..60 minutes.");
        return Safe(async () => {
            var serviceName = service is null ? ".+" : "NexusHR." + (service == "gateway" ? "Gateway" : char.ToUpperInvariant(service[0]) + service[1..] + ".Api");
            var query = "{service_name=~\"" + Regex.Escape(serviceName).Replace("\\", "\\\\") + "\"}";
            if (service is null) query = "{service_name=~\"NexusHR.+\"}";
            if (traceId is not null) query += " | trace_id=\"" + traceId + "\"";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var json = await FetchAsync(options.Loki, $"/loki/api/v1/query_range?query={Uri.EscapeDataString(query)}&limit=100&start={now - (minutes??15)*60}&end={now}", ct);
            var logs = new List<object>();
            foreach (var stream in json.GetProperty("data").GetProperty("result").EnumerateArray()) foreach (var value in stream.GetProperty("values").EnumerateArray()) {
                var metadata = value.GetArrayLength() > 2 ? value[2] : stream.GetProperty("stream");
                var level = metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("severity_text", out var l) ? l.GetString() : "UNKNOWN";
                level=level?.ToUpperInvariant() switch {"INFORMATION"=>"INFO","WARNING"=>"WARN",var normalized=>normalized};
                if (severity is not null && level != severity) continue;
                var trace = metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("trace_id", out var t) && Regex.IsMatch(t.GetString() ?? "", "^[a-f0-9]{32}$") ? t.GetString() : null;
                var type=metadata.ValueKind==JsonValueKind.Object?(String(metadata,"ExceptionType")??String(metadata,"exception_type"))?.ToString():null;
                if(type is not null&&!Regex.IsMatch(type,"^[A-Za-z0-9_.+`]{1,200}$"))type=null;
                logs.Add(new { timestampNs = value[0].GetString(), service = String(stream.GetProperty("stream"), "service_name"), severity = level, traceId = trace,
                    exceptionType=type,
                    message = "Application event (content redacted)", exceptionDetails = "Raw messages/stack traces withheld to protect CVs, SQL values and tokens." });
            }
            return logs;
        });
    }
    public async Task<SourceResult> CiAsync(CancellationToken ct)
    {
        await ciGate.WaitAsync(ct);
        try {
            if (cachedCi is not null && ciExpires > DateTime.UtcNow) return cachedCi;
            cachedCi = await ReadCiAsync(ct); ciExpires = DateTime.UtcNow.AddMinutes(cachedCi.Available ? 5 : 1); return cachedCi;
        } finally { ciGate.Release(); }
    }
    private Task<SourceResult> ReadCiAsync(CancellationToken ct) => Safe(async () => {
        void Auth(HttpRequestMessage r) { r.Headers.UserAgent.ParseAdd("NexusHR-Engineering/1.0"); if (options.GitHubToken.Length > 0) r.Headers.Authorization = new("Bearer", options.GitHubToken); }
        const string root = "https://api.github.com/repos/atakanbektas/NexusHR";
        var runs = await FetchAsync(root, "/actions/runs?branch=main&per_page=8", ct, Auth);
        var releases = await FetchAsync(root, "/releases?per_page=8", ct, Auth);
        var deployments = await FetchAsync(root, "/deployments?per_page=8", ct, Auth);
        var latest = runs.GetProperty("workflow_runs").EnumerateArray().FirstOrDefault();
        var jobs = latest.ValueKind == JsonValueKind.Undefined ? default : await FetchAsync(root, $"/actions/runs/{latest.GetProperty("id").GetInt64()}/jobs", ct, Auth);
        return new { runs = runs.GetProperty("workflow_runs").EnumerateArray().Select(r => new { id = r.GetProperty("id").GetInt64(), name = String(r,"name"), status = String(r,"status"), conclusion = String(r,"conclusion"), sha = String(r,"head_sha"), url = String(r,"html_url"), createdAt = String(r,"created_at") }).ToArray(),
            releases = releases.EnumerateArray().Select(r => new { tag = String(r,"tag_name"), url = String(r,"html_url"), publishedAt = String(r,"published_at") }).ToArray(),
            deployments = deployments.EnumerateArray().Select(r => new { environment = String(r,"environment"), sha = String(r,"sha"), createdAt = String(r,"created_at") }).ToArray(),
            checks = jobs.ValueKind == JsonValueKind.Undefined ? [] : jobs.GetProperty("jobs").EnumerateArray().SelectMany(j => j.GetProperty("steps").EnumerateArray()).Select(s => new { name = String(s,"name"), status = String(s,"status"), conclusion = String(s,"conclusion") }).ToArray(),
            observedAtUtc = DateTime.UtcNow, cachedSeconds = 300, artifactResultsAvailable = false, note = "Run/check conclusions are measured GitHub statuses, not parsed test counts, vulnerability totals or proof of a deployment. No workflow/deployment write API exists here." };
    });
    public Task<SourceResult> AiAsync(string authorization, CancellationToken ct) => Safe(async () => {
        var data = await FetchAsync(ServiceUrl("candidate"), "/api/engineering-ai", ct, r => { if (authorization.Length > 0) r.Headers.TryAddWithoutValidation("Authorization", authorization); });
        return data;
    });
}
