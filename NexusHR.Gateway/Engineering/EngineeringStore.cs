using System.Text.Json;
using Npgsql;

namespace NexusHR.Gateway.Engineering;

// The gateway owns only engineering history. No connection here can mutate business databases.
public sealed class EngineeringStore(EngineeringOptions options) : IAsyncDisposable
{
    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(options.Database);
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand(Schema);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    internal const string Schema = """
        CREATE TABLE IF NOT EXISTS engineering_runs (
          id uuid PRIMARY KEY, actor text NOT NULL, request_key uuid NOT NULL, kind text NOT NULL,
          status text NOT NULL DEFAULT 'Queued', request jsonb NOT NULL, report jsonb,
          created_at timestamptz NOT NULL DEFAULT now(), finished_at timestamptz,
          cancellation_requested boolean NOT NULL DEFAULT false, lease_until timestamptz, lease_id uuid,
          UNIQUE(actor,request_key));
        CREATE INDEX IF NOT EXISTS engineering_runs_created ON engineering_runs(created_at DESC);
        CREATE TABLE IF NOT EXISTS engineering_audit (
          id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, run_id uuid, actor text NOT NULL,
          action text NOT NULL, created_at timestamptz NOT NULL DEFAULT now());
        CREATE TABLE IF NOT EXISTS engineering_agent (id int PRIMARY KEY CHECK(id=1), seen_at timestamptz NOT NULL);
        CREATE TABLE IF NOT EXISTS engineering_inventory (id int PRIMARY KEY CHECK(id=1), observed_at timestamptz NOT NULL, payload jsonb NOT NULL);
        """;
    public async Task<Guid> CreateAsync(string actor, Guid key, ExperimentRequest request, CancellationToken ct)
    {
        await using var c = await source.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        // Global queue budget is serialized across gateway replicas; the unique key is authoritative.
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(774031)", c, tx)) await gate.ExecuteNonQueryAsync(ct);
        var json = JsonSerializer.Serialize(request, Json);
        await using (var existing = new NpgsqlCommand("SELECT id, request::text FROM engineering_runs WHERE actor=@actor AND request_key=@key", c, tx))
        {
            existing.Parameters.AddWithValue("actor", actor); existing.Parameters.AddWithValue("key", key);
            await using var rows = await existing.ExecuteReaderAsync(ct);
            if (await rows.ReadAsync(ct))
            {
                if (!JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(json), JsonSerializer.Deserialize<JsonElement>(rows.GetString(1)))) throw new InvalidOperationException("Idempotency key reused with different parameters.");
                return rows.GetGuid(0);
            }
        }
        await using (var budget = new NpgsqlCommand("SELECT count(*) FROM engineering_runs WHERE status IN ('Queued','Running')", c, tx))
            if ((long)(await budget.ExecuteScalarAsync(ct))! >= 5) throw new InvalidOperationException("Engineering queue is full.");
        var id = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand("INSERT INTO engineering_runs(id,actor,request_key,kind,request) VALUES(@id,@actor,@key,@kind,@request::jsonb); INSERT INTO engineering_audit(run_id,actor,action) VALUES(@id,@actor,'Created')", c, tx);
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("actor", actor); cmd.Parameters.AddWithValue("key", key);
        cmd.Parameters.AddWithValue("kind", request.Kind); cmd.Parameters.AddWithValue("request", json);
        await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return id;
    }
    public async Task<IReadOnlyList<EngineeringRun>> ListAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT id,kind,status,created_at,finished_at,cancellation_requested,request::text,(report-'samples')::text FROM engineering_runs ORDER BY created_at DESC LIMIT 100");
        await using var r = await cmd.ExecuteReaderAsync(ct); var items = new List<EngineeringRun>();
        while (await r.ReadAsync(ct)) items.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDateTime(3),
            r.IsDBNull(4) ? null : r.GetDateTime(4), r.GetBoolean(5), JsonSerializer.Deserialize<JsonElement>(r.GetString(6)),
            r.IsDBNull(7) ? null : JsonSerializer.Deserialize<JsonElement>(r.GetString(7))));
        return items;
    }
    public async Task<bool> CancelAsync(Guid id, string actor, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            WITH changed AS (UPDATE engineering_runs SET cancellation_requested=true,
              status=CASE WHEN status='Queued' THEN 'Cancelled' ELSE status END,
              finished_at=CASE WHEN status='Queued' THEN now() ELSE finished_at END
              WHERE id=@id AND status IN ('Queued','Running') RETURNING id)
            INSERT INTO engineering_audit(run_id,actor,action) SELECT id,@actor,'CancellationRequested' FROM changed
            """);
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("actor", actor);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
    public async Task<RunClaim?> ClaimAsync(CancellationToken ct)
    {
        await HeartbeatAsync(ct);
        await using var c = await source.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(774032)", c, tx)) await gate.ExecuteNonQueryAsync(ct);
        await using (var recover = new NpgsqlCommand("UPDATE engineering_runs SET status='RecoveryRequired' WHERE status='Running' AND lease_until<now()", c, tx)) await recover.ExecuteNonQueryAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            WITH next AS (SELECT id,status AS prior FROM engineering_runs WHERE status IN ('Queued','RecoveryRequired')
              AND NOT EXISTS(SELECT 1 FROM engineering_runs WHERE status='Running')
              ORDER BY CASE WHEN status='RecoveryRequired' THEN 0 ELSE 1 END,created_at LIMIT 1)
            UPDATE engineering_runs r SET status='Running',lease_until=now()+interval '45 seconds',lease_id=@lease
            FROM next WHERE r.id=next.id
            RETURNING r.id,r.kind,r.status,r.created_at,r.finished_at,r.cancellation_requested,r.request::text,r.report::text,next.prior
            """, c, tx);
        var lease = Guid.NewGuid(); cmd.Parameters.AddWithValue("lease", lease);
        RunClaim? item = null;
        await using (var r = await cmd.ExecuteReaderAsync(ct)) if (await r.ReadAsync(ct)) item = new(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDateTime(3), null, r.GetBoolean(5), JsonSerializer.Deserialize<JsonElement>(r.GetString(6)), r.IsDBNull(7) ? null : JsonSerializer.Deserialize<JsonElement>(r.GetString(7))), lease, r.GetString(8) == "RecoveryRequired");
        await tx.CommitAsync(ct); return item;
    }
    public async Task<bool?> ReportAsync(Guid id, Guid lease, RunUpdate report, CancellationToken ct)
    {
        await HeartbeatAsync(ct);
        await using var c = await source.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE engineering_runs SET status=@status,report=@report::jsonb,lease_until=now()+interval '45 seconds',
              finished_at=CASE WHEN @status='Running' THEN NULL ELSE now() END
            WHERE id=@id AND lease_id=@lease AND status='Running' AND lease_until>now() RETURNING cancellation_requested
            """, c, tx);
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("status", report.Status); cmd.Parameters.AddWithValue("report", JsonSerializer.Serialize(report, Json));
        cmd.Parameters.AddWithValue("lease", lease);
        var cancel = await cmd.ExecuteScalarAsync(ct);
        if (cancel is null) return null;
        if (report.Status != "Running")
        {
            await using var audit = new NpgsqlCommand("INSERT INTO engineering_audit(run_id,actor,action) VALUES(@id,'agent',@status)", c, tx);
            audit.Parameters.AddWithValue("id", id); audit.Parameters.AddWithValue("status", report.Status); await audit.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct); return (bool)cancel;
    }
    public async Task<object> AuditAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT run_id, action, created_at FROM engineering_audit ORDER BY id DESC LIMIT 100");
        await using var r = await cmd.ExecuteReaderAsync(ct); var data = new List<object>();
        while (await r.ReadAsync(ct)) data.Add(new { runId = r.IsDBNull(0) ? (Guid?)null : r.GetGuid(0), action = r.GetString(1), at = r.GetDateTime(2) });
        return data; // Subject identifiers remain in the audit database, not exposed to dashboard readers.
    }
    public ValueTask DisposeAsync() => source.DisposeAsync();
    public async Task SaveInventoryAsync(InventoryUpdate data, CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("INSERT INTO engineering_inventory(id,observed_at,payload) VALUES(1,now(),@data::jsonb) ON CONFLICT(id) DO UPDATE SET observed_at=now(),payload=@data::jsonb");
        cmd.Parameters.AddWithValue("data",JsonSerializer.Serialize(data,Json));await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<SourceResult> InventoryAsync(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT observed_at,payload::text FROM engineering_inventory WHERE id=1");await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct)||r.GetDateTime(0)<DateTime.UtcNow.AddSeconds(-45)) return new(false,null,"No fresh privileged metadata snapshot. Start the explicit local agent.");
        return new(true,new { observedAtUtc=r.GetDateTime(0), containers=JsonSerializer.Deserialize<InventoryUpdate>(r.GetString(1),Json)!.Containers });
    }
    private async Task HeartbeatAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("INSERT INTO engineering_agent(id,seen_at) VALUES(1,now()) ON CONFLICT(id) DO UPDATE SET seen_at=now()");
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<bool?> RenewAsync(Guid id,Guid lease,CancellationToken ct)
    {
        await HeartbeatAsync(ct);
        await using var cmd=source.CreateCommand("UPDATE engineering_runs SET lease_until=now()+interval '45 seconds' WHERE id=@id AND lease_id=@lease AND status='Running' AND lease_until>now() RETURNING cancellation_requested");
        cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("lease",lease);
        return await cmd.ExecuteScalarAsync(ct) is bool cancel ? cancel : null;
    }
    public async Task<DateTime?> AgentSeenAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT seen_at FROM engineering_agent WHERE id=1");
        return await cmd.ExecuteScalarAsync(ct) is DateTime value ? value : null;
    }
    public async Task<EngineeringRun?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT id,kind,status,created_at,finished_at,cancellation_requested,request::text,report::text FROM engineering_runs WHERE id=@id");
        cmd.Parameters.AddWithValue("id", id); await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetDateTime(3),r.IsDBNull(4)?null:r.GetDateTime(4),r.GetBoolean(5),JsonSerializer.Deserialize<JsonElement>(r.GetString(6)),r.IsDBNull(7)?null:JsonSerializer.Deserialize<JsonElement>(r.GetString(7))) : null;
    }
}
