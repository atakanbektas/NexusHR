import { useEffect, useState, useRef } from "react";
import { Link, useLocation } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  ScopedCssBaseline,
  FormControlLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  ThemeProvider,
  Typography,
  createTheme,
} from "@mui/material";
import { candidateApi } from "../../api/candidateApi";
import { hasAnyRole } from "../../auth/roles";
import "./engineering-print.css";

type Row = Record<string, unknown>;
type Source<T> = { available: boolean; data: T | null; error?: string };
type Sample = {
  elapsedSeconds: number;
  requests: number;
  failures: number;
  timeouts: number;
  rps: number;
  averageMs: number;
  p50Ms: number;
  p95Ms: number;
  p99Ms: number;
  cpuPercent: number | null;
  memoryBytes: number | null;
  databaseConnections: number | null;
  queueDepth: number | null;
};
type Step = {
  vus: number;
  passed: boolean;
  outcome: string;
  metrics: Sample;
  httpStatuses: Record<string, number>;
};
type Report = {
  status: string;
  code: string;
  progress: number;
  samples?: Sample[];
  steps: Step[];
  checks: Record<string, boolean>;
  recoverySeconds?: number;
  benchmarks?: object[];
};
type Run = {
  id: string;
  kind: string;
  status: string;
  createdAtUtc: string;
  cancellationRequested: boolean;
  request: { vus: number; target: string; scenario: string };
  report?: Report;
};
type Capabilities = {
  executionEnabled: boolean;
  maxVus: number;
  agentSeenAtUtc: string | null;
  agentOnline: boolean;
  presets: number[];
  benchmarkModels: string[];
};
type Series = {
  labels: Record<string, string>;
  values: [number, number | null][];
};
const sections = [
  "Overview",
  "Load Testing",
  "Infrastructure",
  "Distributed Tracing",
  "Database Performance",
  "Message Broker",
  "Logs & Errors",
  "Chaos Engineering",
  "CI/CD & Releases",
  "AI Observability",
];
const slugs = [
  "overview",
  "load-testing",
  "infrastructure",
  "tracing",
  "database",
  "broker",
  "logs",
  "chaos",
  "ci",
  "ai",
];
function useResource<T>(path: string | null, interval = 10000) {
  const [state, setState] = useState<{ path: string; data: T }>();
  const [failure, setFailure] = useState<{ path: string; message: string }>();
  useEffect(() => {
    if (!path) return;
    const controller = new AbortController();
    let inProgress = false;
    const load = async () => {
      if (inProgress) return;
      inProgress = true;
      try {
        const response = await candidateApi.get<T>(`/api/engineering/${path}`, {
          signal: controller.signal,
          timeout: 15000,
        });
        if (!controller.signal.aborted) {
          setState({ path, data: response.data });
          setFailure(undefined);
        }
      } catch {
        if (!controller.signal.aborted)
          setFailure({
            path,
            message:
              "Source unavailable, module disabled, or access denied. No values were fabricated.",
          });
      } finally {
        inProgress = false;
      }
    };
    void load();
    const timer = setInterval(() => {
      void load();
    }, interval);
    return () => {
      controller.abort();
      clearInterval(timer);
    };
  }, [path, interval]);
  return {
    data:
      state?.path === path && failure?.path !== path ? state.data : undefined,
    error: failure?.path === path ? failure.message : "",
  };
}
const format = (v: unknown): string =>
  v === null || v === undefined
    ? "Not available"
    : typeof v === "number"
      ? v.toLocaleString(undefined, { maximumFractionDigits: 2 })
      : typeof v === "object"
        ? JSON.stringify(v)
        : String(v);
function Rows({ rows }: { rows: object[] }) {
  if (!rows.length)
    return (
      <Alert severity="info">No records in this observation window.</Alert>
    );
  const columns = [...new Set(rows.flatMap((row) => Object.keys(row)))];
  return (
    <Box sx={{ overflowX: "auto" }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            {columns.map((c) => (
              <TableCell key={c}>{c}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row, i) => (
            <TableRow key={i}>
              {columns.map((c) => (
                <TableCell
                  key={c}
                  sx={{ maxWidth: 420, overflowWrap: "anywhere" }}
                >
                  {format((row as Row)[c])}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}
function Pending({ error }: { error: string }) {
  return error ? (
    <Alert severity="warning">{error}</Alert>
  ) : (
    <CircularProgress aria-label="Loading measured data" size={24} />
  );
}
function SourceRows({
  source,
  error,
}: {
  source?: Source<object[]>;
  error: string;
}) {
  return !source ? (
    <Pending error={error} />
  ) : !source.available ? (
    <Alert severity="warning">{source.error}</Alert>
  ) : (
    <Rows rows={source.data ?? []} />
  );
}
function Metric({
  name,
  title,
  unit,
}: {
  name: string;
  title: string;
  unit: string;
}) {
  const { data, error } = useResource<Source<Series[]>>(`metrics/${name}`);
  const colors = ["#53d8fb", "#a5e887", "#f8c86a", "#e39bff", "#fb8f91"];
  const series = data?.data ?? [];
  const values = series.flatMap((s) =>
    s.values.map((v) => v[1]).filter((v): v is number => v !== null),
  );
  const max = Math.max(1, ...values),
    min = Math.min(0, ...values);
  return (
    <Paper variant="outlined" sx={{ p: 2, minWidth: 0 }}>
      <Typography sx={{ fontWeight: 700 }}>
        {title}{" "}
        <Typography component="span" color="text.secondary" variant="caption">
          {unit} · last 15 min
        </Typography>
      </Typography>
      {!data ? (
        <Pending error={error} />
      ) : !data.available ? (
        <Alert severity="warning">{data.error}</Alert>
      ) : !values.length ? (
        <Alert severity="info">
          No samples. Check exporter readiness and generate real requests.
        </Alert>
      ) : (
        <>
          <svg
            viewBox="0 0 600 150"
            role="img"
            aria-label={`${title} measured time series`}
            style={{ width: "100%", height: 150 }}
          >
            {series.map((s, index) => (
              <path
                key={index}
                d={s.values.reduce(
                  (path, v, i) =>
                    v[1] === null
                      ? path
                      : `${path}${i === 0 || s.values[i - 1][1] === null ? "M" : "L"}${10 + (i / Math.max(1, s.values.length - 1)) * 580},${140 - ((v[1] - min) / (max - min)) * 125}`,
                  "",
                )}
                stroke={colors[index % colors.length]}
                fill="none"
                strokeWidth={2}
              />
            ))}
          </svg>
          <Stack spacing={0.5}>
            {series.map((s, i) => (
              <Typography
                key={i}
                variant="caption"
                sx={{ color: colors[i % colors.length] }}
              >
                {Object.values(s.labels).join(" / ") || "aggregate"}:{" "}
                {format(s.values.at(-1)?.[1])} {unit}
              </Typography>
            ))}
          </Stack>
        </>
      )}
    </Paper>
  );
}
function Overview() {
  const { data, error } = useResource<{
    observedAtUtc: string;
    services: { service: string; healthy: boolean; latencyMs: number }[];
  }>("overview");
  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Live metadata only. Engineering roles do not grant candidate or CV
        access. Polling: 10 seconds.
      </Alert>
      {!data ? (
        <Pending error={error} />
      ) : (
        <Paper sx={{ p: 2 }}>
          <Typography variant="caption">
            Observed {new Date(data.observedAtUtc).toLocaleString()}
          </Typography>
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ flexWrap: "wrap", my: 2 }}
          >
            {data.services.map((s) => (
              <Chip
                key={s.service}
                label={`${s.service}: ${s.healthy ? "Ready" : "Unavailable"} · ${s.latencyMs.toFixed(0)} ms probe`}
                color={s.healthy ? "success" : "error"}
              />
            ))}
          </Stack>
        </Paper>
      )}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "1fr 1fr" },
          gap: 2,
        }}
      >
        <Metric name="requests" title="HTTP requests" unit="req/s" />
        <Metric name="latency" title="HTTP p95" unit="ms" />
      </Box>
    </Stack>
  );
}
function Infrastructure() {
  const inventory =
    useResource<Source<{ observedAtUtc: string; containers: object[] }>>(
      "infrastructure",
    );
  return (
    <Stack spacing={2}>
      <Typography variant="h6">
        Running images, container state and engine restart counters
      </Typography>
      {!inventory.data ? (
        <Pending error={inventory.error} />
      ) : !inventory.data.available ? (
        <Alert severity="warning">{inventory.data.error}</Alert>
      ) : (
        <Rows rows={inventory.data.data?.containers ?? []} />
      )}
      <Alert severity="info">
        Host panels describe the Docker Linux VM, not Windows. Container CPU is
        percent of one core. CPU values may exceed 100%.
      </Alert>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "1fr 1fr" },
          gap: 2,
        }}
      >
        {[
          ["cpu", "Container CPU", "% / core"],
          ["memory", "Working set", "bytes"],
          ["network", "Receive traffic", "bytes/s"],
          ["restarts", "Start-time changes", "changes / 15 min"],
          ["host-memory", "Docker VM used RAM", "bytes"],
          ["disk", "Filesystem used space", "bytes"],
          ["errors", "HTTP server errors", "errors/s"],
        ].map(([name, title, unit]) => (
          <Metric key={name} name={name} title={title} unit={unit} />
        ))}
      </Box>
    </Stack>
  );
}
function Database() {
  const [service, setService] = useState("candidate");
  const { data, error } = useResource<
    Source<{
      stats: Row;
      indexes: object[];
      statementsAvailable: boolean;
      statements: object[];
      planLimit: string;
    }>
  >(`databases/${service}`);
  return (
    <Stack spacing={2}>
      <TextField
        select
        label="Database"
        value={service}
        onChange={(e) => setService(e.target.value)}
      >
        {["candidate", "hiring", "onboarding"].map((s) => (
          <MenuItem key={s} value={s}>
            {s}
          </MenuItem>
        ))}
      </TextField>
      {!data ? (
        <Pending error={error} />
      ) : !data.available ? (
        <Alert severity="warning">{data.error}</Alert>
      ) : (
        data.data && (
          <>
            <Typography variant="h6">
              Database statistics · cumulative counters since PostgreSQL reset
            </Typography>
            <Rows rows={[data.data.stats]} />
            <Typography variant="h6">Index usage</Typography>
            <Rows rows={data.data.indexes} />
            <Typography variant="h6">
              Query fingerprints · no SQL text or literals
            </Typography>
            {data.data.statementsAvailable ? (
              <Rows rows={data.data.statements} />
            ) : (
              <Alert severity="warning">
                pg_stat_statements is not enabled. No slow-query measurements
                are available. Enable it through a planned database maintenance
                procedure.
              </Alert>
            )}
            <Alert severity="info">{data.data.planLimit}</Alert>
          </>
        )
      )}
      <Metric
        name="pool"
        title="Application connection pools"
        unit="connections"
      />
    </Stack>
  );
}
function Broker() {
  const { data, error } = useResource<
    Source<{
      queues: object[];
      exchanges: object[];
      truncated: boolean;
      retryNote: string;
      messageStores: { service: string; source: Source<object> }[];
    }>
  >("broker");
  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Metadata only. Message bodies, replay and broker mutation are not
        exposed.
      </Alert>
      {!data ? (
        <Pending error={error} />
      ) : !data.available ? (
        <Alert severity="warning">{data.error}</Alert>
      ) : (
        data.data && (
          <>
            <Typography variant="h6">
              Queues / error / skipped queues
            </Typography>
            {data.data.truncated && (
              <Alert severity="warning">First 100 queues shown.</Alert>
            )}
            <Rows rows={data.data.queues} />
            <Typography variant="h6">
              Transactional Outbox / Inbox metadata
            </Typography>
            <Rows
              rows={data.data.messageStores.map((s) => ({
                service: s.service,
                available: s.source.available,
                ...(s.source.data ?? {}),
                note: s.source.error,
              }))}
            />
            <Typography variant="h6">Exchanges</Typography>
            <Rows rows={data.data.exchanges} />
            <Alert severity="info">{data.data.retryNote}</Alert>
          </>
        )
      )}
    </Stack>
  );
}
function Traces() {
  const list =
    useResource<
      Source<{ traceId: string; service: string; durationMs: number }[]>
    >("traces");
  const [id, setId] = useState("");
  const detail = useResource<
    Source<{ spans: object[]; criticalPathNote: string }>
  >(/^[a-f0-9]{32}$/.test(id) ? `traces/${id}` : null);
  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Trace ID also serves as the request correlation ID. Operation names and
        attributes are redacted before storage.
      </Alert>
      <TextField
        label="Trace / correlation ID"
        value={id}
        onChange={(e) => setId(e.target.value)}
      />
      {!list.data ? (
        <Pending error={list.error} />
      ) : !list.data.available ? (
        <Alert severity="warning">{list.data.error}</Alert>
      ) : (
        <Stack spacing={1}>
          {list.data.data?.map((t) => (
            <Button
              key={t.traceId}
              variant="outlined"
              onClick={() => setId(t.traceId)}
              sx={{ justifyContent: "space-between", textTransform: "none" }}
            >
              {t.service} · {t.traceId} · {format(t.durationMs)} ms
            </Button>
          ))}
        </Stack>
      )}
      {id &&
        (detail.data?.available && detail.data.data ? (
          <>
            <Rows rows={detail.data.data.spans} />
            <Alert severity="info">{detail.data.data.criticalPathNote}</Alert>
            <Button component={Link} to={`/engineering/logs?traceId=${id}`}>
              Related logs
            </Button>
          </>
        ) : (
          <Pending error={detail.error || detail.data?.error || ""} />
        ))}
    </Stack>
  );
}
function Logs() {
  const [service, setService] = useState("");
  const [minutes, setMinutes] = useState(15);
  const [severity, setSeverity] = useState("");
  const [trace, setTrace] = useState(
    new URLSearchParams(useLocation().search).get("traceId") ?? "",
  );
  const query = new URLSearchParams();
  query.set("minutes", String(minutes));
  if (service) query.set("service", service);
  if (severity) query.set("severity", severity);
  if (/^[a-f0-9]{32}$/.test(trace)) query.set("traceId", trace);
  const { data, error } = useResource<Source<object[]>>(`logs?${query}`);
  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Last {minutes} minutes, at most 100 events. Raw log messages and
        exception stacks are withheld; the collector removes sensitive contents
        before Loki storage.
      </Alert>
      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        <TextField
          select
          label="Time window"
          value={minutes}
          onChange={(e) => setMinutes(Number(e.target.value))}
          sx={{ minWidth: 150 }}
        >
          {[5, 15, 30, 60].map((n) => (
            <MenuItem key={n} value={n}>
              {n} minutes
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Service"
          value={service}
          onChange={(e) => setService(e.target.value)}
          sx={{ minWidth: 170 }}
        >
          <MenuItem value="">All</MenuItem>
          {["gateway", "candidate", "hiring", "onboarding", "notification"].map(
            (s) => (
              <MenuItem key={s} value={s}>
                {s}
              </MenuItem>
            ),
          )}
        </TextField>
        <TextField
          select
          label="Severity"
          value={severity}
          onChange={(e) => setSeverity(e.target.value)}
          sx={{ minWidth: 130 }}
        >
          <MenuItem value="">All</MenuItem>
          {["INFO", "WARN", "ERROR", "DEBUG"].map((s) => (
            <MenuItem key={s} value={s}>
              {s}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          label="Correlation ID"
          value={trace}
          onChange={(e) => setTrace(e.target.value)}
        />
      </Stack>
      <SourceRows source={data} error={error} />
    </Stack>
  );
}
function Ci() {
  const { data, error } = useResource<
    Source<{
      runs: object[];
      checks: object[];
      releases: object[];
      deployments: object[];
      note: string;
      observedAtUtc: string;
    }>
  >("ci", 30000);
  return !data ? (
    <Pending error={error} />
  ) : !data.available ? (
    <Alert severity="warning">{data.error}</Alert>
  ) : (
    data.data && (
      <Stack spacing={2}>
        <Alert severity="info">
          {data.data.note} Snapshot{" "}
          {new Date(data.data.observedAtUtc).toLocaleString()}, cached up to
          five minutes.
        </Alert>
        {(["runs", "checks", "releases", "deployments"] as const).map((key) => (
          <Box key={key}>
            <Typography variant="h6">{key}</Typography>
            <Rows rows={data.data![key]} />
          </Box>
        ))}
      </Stack>
    )
  );
}
function Ai() {
  const { data, error } = useResource<
    Source<{
      enabled: boolean;
      model: string;
      provider: string;
      states: Row;
      attempts: Row;
      modelDigest: string | null;
      nativeOutputTokensPerSecond: number | null;
      failedWithUnknownCode: number;
      evidenceProposed: number | null;
      evidenceRejected: number | null;
      errorCodes: Row;
    }>
  >("ai");
  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Evidence validity measures alignment with source text, not extraction
        accuracy or truth. Human source review remains necessary. Synthetic
        benchmarks below report literal gold-marker coverage, not comprehensive
        accuracy.
      </Alert>
      {!data ? (
        <Pending error={error} />
      ) : !data.available ? (
        <Alert severity="warning">{data.error}</Alert>
      ) : (
        data.data && (
          <>
            <Typography variant="h6">
              {data.data.provider} · {data.data.model} ·{" "}
              {data.data.enabled ? "Enabled" : "Disabled"}
            </Typography>
            <Rows rows={[data.data.states]} />
            <Typography>
              Attempts in the last 24 hours (bounded sample). Effective token
              throughput includes the full pipeline; native speed uses actual
              provider eval duration where recorded. Earlier unclassified
              failures remain unknown, not zero historical errors.
            </Typography>
            <Rows rows={[data.data.attempts]} />
            <Rows
              rows={[
                {
                  modelDigest: data.data.modelDigest,
                  nativeTokensPerSecond: data.data.nativeOutputTokensPerSecond,
                  failedWithUnknownCode: data.data.failedWithUnknownCode,
                  evidenceProposed: data.data.evidenceProposed,
                  evidenceRejected: data.data.evidenceRejected,
                  errorCodes: data.data.errorCodes,
                },
              ]}
            />
          </>
        )
      )}
      <Typography variant="h6">
        Synthetic model benchmark · isolated lab
      </Typography>
      <Experiments chaos={false} benchmarkOnly />
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "1fr 1fr" },
          gap: 2,
        }}
      >
        {[
          ["ai-duration", "Attempt p95", "s"],
          ["ai-attempts", "Processed attempts", "attempts"],
          ["ai-evidence", "Evidence dispositions", "facts"],
          ["ai-tokens", "Provider tokens", "tokens"],
        ].map(([name, title, unit]) => (
          <Metric key={name} name={name} title={title} unit={unit} />
        ))}
      </Box>
    </Stack>
  );
}
function Experiments({
  chaos,
  benchmarkOnly = false,
}: {
  chaos: boolean;
  benchmarkOnly?: boolean;
}) {
  const cap = useResource<Capabilities>("capabilities", 5000);
  const history = useResource<Run[]>("runs", 3000);
  const [selected, setSelected] = useState("");
  const matchingRuns = (history.data ?? []).filter((r) =>
    benchmarkOnly
      ? r.kind === "ai-benchmark"
      : chaos
        ? r.kind === "chaos"
        : ["load", "capacity"].includes(r.kind),
  );
  const selectedId = selected || matchingRuns[0]?.id;
  const detail = useResource<Run>(
    selectedId ? `runs/${selectedId}` : null,
    2000,
  );
  const [kind, setKind] = useState(
    benchmarkOnly ? "ai-benchmark" : chaos ? "chaos" : "load",
  );
  const [target, setTarget] = useState(
    benchmarkOnly ? "candidates" : "overview",
  );
  const [models, setModels] = useState(["qwen3:4b", "qwen2.5vl:3b"]);
  const [scenario, setScenario] = useState(
    chaos ? "restart-candidate" : "constant",
  );
  const [vus, setVus] = useState(5);
  const [duration, setDuration] = useState(15);
  const [ramp, setRamp] = useState(0);
  const [rate, setRate] = useState(10);
  const [levels, setLevels] = useState("1,5,10");
  const [p95, setP95] = useState(1000);
  const [errorRate, setErrorRate] = useState(1);
  const [cpu, setCpu] = useState(90);
  const [fault, setFault] = useState(5);
  const [latency, setLatency] = useState(100);
  const [confirmed, setConfirmed] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [compare, setCompare] = useState<string[]>([]);
  const pendingRequest = useRef<{ payload: string; key: string } | null>(null);
  const operator = hasAnyRole(["EngineeringOperator"]);
  const online = cap.data?.agentOnline;
  const numeric = (
    label: string,
    value: number,
    setter: (v: number) => void,
    min: number,
    max: number,
  ) => (
    <TextField
      type="number"
      label={label}
      value={value}
      onChange={(e) => setter(Number(e.target.value))}
      slotProps={{ htmlInput: { min, max } }}
    />
  );
  async function start() {
    setBusy(true);
    setMessage("");
    try {
      const body = {
        kind,
        target,
        scenario,
        vus: benchmarkOnly ? 1 : vus,
        durationSeconds: duration,
        rampSeconds: ramp,
        arrivalRate: rate,
        ...(benchmarkOnly ? { models } : {}),
        ...(kind === "capacity"
          ? { levels: levels.split(",").map(Number) }
          : {}),
        maxErrorRate: errorRate / 100,
        maxP95Ms: p95,
        maxCpuPercent: cpu,
        confirmation: confirmed ? "SYNTHETIC LAB ONLY" : "",
        faultSeconds: fault,
        latencyMs: latency,
      };
      const payload = JSON.stringify(body);
      if (pendingRequest.current?.payload !== payload)
        pendingRequest.current = { payload, key: crypto.randomUUID() };
      const response = await candidateApi.post<{ id: string }>(
        "/api/engineering/runs",
        body,
        { headers: { "Idempotency-Key": pendingRequest.current.key } },
      );
      setSelected(response.data.id);
      pendingRequest.current = null;
      setMessage(
        "Experiment queued. All targets belong to the separate synthetic lab.",
      );
    } catch {
      setMessage(
        "Experiment rejected. Check authorization, server budgets, queue capacity and lab readiness.",
      );
    } finally {
      setBusy(false);
    }
  }
  async function cancel(id: string) {
    try {
      await candidateApi.post(`/api/engineering/runs/${id}/cancel`);
      setMessage(
        "Cancellation requested; cleanup and recovery are verified before completion.",
      );
    } catch {
      setMessage("Run is no longer cancellable or access was denied.");
    }
  }
  async function download(format: string) {
    try {
      const response = await candidateApi.get(
        `/api/engineering/runs/${selectedId}/export?format=${format}`,
        { responseType: "blob" },
      );
      const url = URL.createObjectURL(response.data);
      const a = document.createElement("a");
      a.href = url;
      a.download = `engineering-${selectedId}.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      setMessage("Export unavailable.");
    }
  }
  const report = detail.data?.report;
  return (
    <Stack spacing={2}>
      <Alert severity="warning">
        Isolated development lab only. Presets are configuration choices, not
        sustainable-capacity promises. Current server budget:{" "}
        {cap.data?.maxVus ?? "unavailable"} VUs. Agent:{" "}
        {online ? "connected" : "offline / unknown"}. Default request budget:
        10,000 per step. CPU SLO requires three measured samples.
      </Alert>
      {!cap.data ? (
        <Pending error={cap.error} />
      ) : (
        <Paper sx={{ p: 2 }}>
          {benchmarkOnly && (
            <>
              <Alert severity="info">
                Fixed public bilingual synthetic PDF only. Model weights are
                read-only; main AI settings are unchanged. Missing models fail
                explicitly; no automatic download. Gold-marker coverage is not
                comprehensive extraction accuracy.
              </Alert>
              <Stack direction="row">
                {cap.data.benchmarkModels.map((model) => (
                  <FormControlLabel
                    key={model}
                    control={
                      <Checkbox
                        checked={models.includes(model)}
                        onChange={(e) =>
                          setModels(
                            e.target.checked
                              ? [...models, model]
                              : models.filter((m) => m !== model),
                          )
                        }
                      />
                    }
                    label={model}
                  />
                ))}
              </Stack>
            </>
          )}
          <Box sx={{ display: benchmarkOnly ? "none" : "contents" }}>
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", md: "repeat(3,1fr)" },
                gap: 2,
              }}
            >
              {!chaos && (
                <TextField
                  select
                  label="Experiment"
                  value={kind}
                  onChange={(e) => setKind(e.target.value)}
                >
                  <MenuItem value="load">Single load</MenuItem>
                  <MenuItem value="capacity">
                    Progressive SLO exploration
                  </MenuItem>
                </TextField>
              )}
              <TextField
                select
                label="Allowlisted target"
                value={target}
                onChange={(e) => setTarget(e.target.value)}
              >
                {[
                  ["overview", "Gateway → Candidate/Hiring list"],
                  ["candidates", "Candidate synthetic create"],
                  ["health", "Gateway readiness"],
                ].map(([key, label]) => (
                  <MenuItem key={key} value={key}>
                    {label}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Scenario"
                value={scenario}
                onChange={(e) => setScenario(e.target.value)}
              >
                {(chaos
                  ? [
                      "restart-candidate",
                      "interrupt-broker",
                      "interrupt-database",
                      "latency",
                    ]
                  : ["constant", "arrival"]
                ).map((s) => (
                  <MenuItem key={s} value={s}>
                    {s}
                  </MenuItem>
                ))}
              </TextField>
              {!chaos && (
                <>
                  <TextField
                    select
                    label="Virtual users"
                    value={vus}
                    onChange={(e) => setVus(Number(e.target.value))}
                  >
                    {[...new Set([1, 5, 10, ...cap.data.presets])].map((v) => (
                      <MenuItem
                        key={v}
                        value={v}
                        disabled={v > cap.data!.maxVus}
                      >
                        {v.toLocaleString()}
                        {v > cap.data!.maxVus ? " · exceeds server budget" : ""}
                      </MenuItem>
                    ))}
                  </TextField>
                  {numeric("Duration (s)", duration, setDuration, 5, 120)}
                  {numeric("Ramp up/down (s)", ramp, setRamp, 0, 20)}
                  {scenario === "arrival" &&
                    numeric(
                      "Arrival rate (requests/s)",
                      rate,
                      setRate,
                      1,
                      1000,
                    )}
                  {kind === "capacity" && (
                    <TextField
                      label="Increasing VU levels"
                      value={levels}
                      onChange={(e) => setLevels(e.target.value)}
                    />
                  )}
                  {numeric("P95 SLO (ms)", p95, setP95, 1, 60000)}
                  {numeric("Error SLO (%)", errorRate, setErrorRate, 0, 100)}
                  {numeric("Sustained CPU SLO (% quota)", cpu, setCpu, 1, 100)}
                </>
              )}
              {chaos && (
                <>
                  {numeric("Fault duration (s)", fault, setFault, 1, 15)}
                  {numeric(
                    "Injected latency (ms)",
                    latency,
                    setLatency,
                    1,
                    1000,
                  )}
                </>
              )}
            </Box>
          </Box>
          <FormControlLabel
            control={
              <Checkbox
                checked={confirmed}
                onChange={(e) => setConfirmed(e.target.checked)}
              />
            }
            label="I approve this experiment in the disposable SYNTHETIC LAB ONLY"
          />
          <Button
            variant="contained"
            disabled={
              !operator ||
              !cap.data.executionEnabled ||
              !online ||
              !confirmed ||
              busy
            }
            onClick={() => {
              void start();
            }}
          >
            Start experiment
          </Button>
          {!operator && (
            <Typography variant="caption">
              EngineeringOperator is required to execute or cancel.
            </Typography>
          )}
        </Paper>
      )}
      {message && <Alert severity="info">{message}</Alert>}
      <Typography variant="h6">History · latest 100 runs</Typography>
      {history.error && <Alert severity="warning">{history.error}</Alert>}
      <Box sx={{ overflowX: "auto" }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Compare (two)</TableCell>
              <TableCell>Run</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Scenario</TableCell>
              <TableCell>Created</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {matchingRuns.map((run) => (
              <TableRow key={run.id} selected={run.id === selectedId}>
                <TableCell>
                  <Checkbox
                    checked={compare.includes(run.id)}
                    onChange={(e) =>
                      setCompare(
                        e.target.checked
                          ? [...compare.slice(-1), run.id]
                          : compare.filter((id) => id !== run.id),
                      )
                    }
                    slotProps={{ input: { "aria-label": `Compare ${run.id}` } }}
                  />
                </TableCell>
                <TableCell>
                  <Button onClick={() => setSelected(run.id)}>
                    {run.id.slice(0, 8)}
                  </Button>
                </TableCell>
                <TableCell>
                  {run.status}
                  {run.cancellationRequested && " · cancelling"}
                </TableCell>
                <TableCell>
                  {run.kind} / {run.request.scenario}
                </TableCell>
                <TableCell>
                  {new Date(run.createdAtUtc).toLocaleString()}
                </TableCell>
                <TableCell>
                  {operator && ["Queued", "Running"].includes(run.status) && (
                    <Button
                      color="warning"
                      onClick={() => {
                        void cancel(run.id);
                      }}
                    >
                      Cancel
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
      {compare.length === 2 && (
        <>
          <Typography variant="h6">Measured comparison</Typography>
          <Rows
            rows={(history.data ?? [])
              .filter((r) => compare.includes(r.id))
              .flatMap(
                (r) =>
                  r.report?.benchmarks?.map((b) => ({
                    run: r.id.slice(0, 8),
                    ...b,
                  })) ??
                  r.report?.steps?.map((s) => ({
                    run: r.id.slice(0, 8),
                    vus: s.vus,
                    outcome: s.outcome,
                    ...s.metrics,
                  })) ??
                  [],
              )}
          />
        </>
      )}
      {detail.data && (
        <Paper id="engineering-report" sx={{ p: 2 }}>
          <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
            <Typography variant="h6">
              Selected experiment · {detail.data.status} /{" "}
              {report?.code ?? "Queued"}
            </Typography>
            <Button
              onClick={() => {
                void download("json");
              }}
            >
              Export JSON
            </Button>
            <Button
              onClick={() => {
                void download("csv");
              }}
            >
              Export CSV
            </Button>
            <Button onClick={() => window.print()}>
              Print performance report
            </Button>
          </Stack>
          <LinearProgress
            variant="determinate"
            value={report?.progress ?? 0}
            sx={{ my: 2 }}
          />
          {report && (
            <>
              {report.benchmarks && <Rows rows={report.benchmarks} />}
              <Rows rows={[detail.data.request]} />
              {!report.benchmarks && (
                <>
                  <Typography>
                    Last tested passing load:{" "}
                    {report.steps.filter((s) => s.passed).at(-1)?.vus ?? "none"}{" "}
                    · First observed SLO breach:{" "}
                    {report.steps.find((s) => s.outcome === "SloBreach")?.vus ??
                      "not observed"}
                    . These are observations, not absolute capacity.
                  </Typography>
                  <Rows
                    rows={report.steps.map((s) => ({
                      vus: s.vus,
                      outcome: s.outcome,
                      ...s.metrics,
                      httpStatuses: s.httpStatuses,
                    }))}
                  />
                  <Typography variant="h6" sx={{ mt: 2 }}>
                    Live / retained samples
                  </Typography>
                  <Rows rows={report.samples?.slice(-10) ?? []} />
                </>
              )}
              <Typography sx={{ mt: 2 }}>
                Cleanup/evidence: {format(report.checks)}
                {report.recoverySeconds !== undefined &&
                  ` · Recovery: ${format(report.recoverySeconds)} s`}
              </Typography>
            </>
          )}
        </Paper>
      )}
    </Stack>
  );
}
export default function EngineeringPage() {
  const slug = useLocation().pathname.split("/")[2] || "overview";
  const section = Math.max(0, slugs.indexOf(slug));
  const [dark, setDark] = useState(true);
  const theme = createTheme({
    palette: {
      mode: dark ? "dark" : "light",
      primary: { main: dark ? "#53d8fb" : "#00677e" },
    },
    shape: { borderRadius: 10 },
  });
  const content = [
    <Overview />,
    <Experiments chaos={false} />,
    <Infrastructure />,
    <Traces />,
    <Database />,
    <Broker />,
    <Logs />,
    <Experiments chaos />,
    <Ci />,
    <Ai />,
  ][section];
  return (
    <ThemeProvider theme={theme}>
      <ScopedCssBaseline>
        <Box
          sx={{
            bgcolor: "background.default",
            minHeight: "calc(100vh - 64px)",
            p: { xs: 1, md: 3 },
          }}
        >
          <Stack
            direction="row"
            sx={{
              justifyContent: "space-between",
              alignItems: "center",
              mb: 3,
            }}
          >
            <Box>
              <Typography variant="overline" color="primary">
                NEXUSHR / ENGINEERING
              </Typography>
              <Typography variant="h4" sx={{ fontWeight: 800 }}>
                Control Center
              </Typography>
              <Typography color="text.secondary">
                Observe evidence. Diagnose dependencies. Execute bounded
                experiments.
              </Typography>
            </Box>
            <FormControlLabel
              control={
                <Checkbox
                  checked={dark}
                  onChange={(e) => setDark(e.target.checked)}
                />
              }
              label="Dark mode"
            />
          </Stack>
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", md: "220px minmax(0,1fr)" },
              gap: 3,
            }}
          >
            <Paper
              component="nav"
              aria-label="Engineering navigation"
              sx={{
                p: 1,
                alignSelf: "start",
                position: { md: "sticky" },
                top: 80,
              }}
            >
              <Stack spacing={0.5}>
                {sections.map((name, i) => (
                  <Button
                    key={name}
                    component={Link}
                    to={`/engineering/${slugs[i]}`}
                    variant={i === section ? "contained" : "text"}
                    sx={{ justifyContent: "flex-start", textTransform: "none" }}
                  >
                    {name}
                  </Button>
                ))}
              </Stack>
            </Paper>
            <Box key={slug} sx={{ minWidth: 0 }}>
              <Typography variant="h5" sx={{ mb: 2 }}>
                {sections[section]}
              </Typography>
              {content}
            </Box>
          </Box>
        </Box>
      </ScopedCssBaseline>
    </ThemeProvider>
  );
}
