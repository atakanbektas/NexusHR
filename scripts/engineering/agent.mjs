import { spawn } from "node:child_process";
import { mkdir, open, stat, readFile } from "node:fs/promises";
import { resolve } from "node:path";
import {
  directory,
  project,
  docker,
  command,
  compose,
  containers,
  proxy,
  recoverLab,
  token,
  available,
  probe,
  sleep,
} from "./lab.mjs";
import { benchmark } from "./benchmark.mjs";
import {
  createSentinel,
  sentinelPreserved,
  verifyDuringOutage,
  messageDelivered,
} from "./sentinel.mjs";
const access = JSON.parse(
  await readFile(resolve(directory, "access.json"), "utf8"),
);
const key = access.agent;
const approvedMaxVus = Math.min(
  10000,
  Number(process.env.ENGINEERING_MAX_VUS ?? 100),
);
if (!Number.isInteger(approvedMaxVus) || approvedMaxVus < 1)
  throw new Error("Invalid agent budget");
const root = "http://127.0.0.1:5000/api/engineering-agent";
const image =
  "grafana/k6@sha256:3e4186aea62e11a45560bfc51e19eeafe82fc07ce069c78aff44b7ff893d9a5e";
let stopping = false;
process.on("SIGINT", () => {
  stopping = true;
});
process.on("SIGTERM", () => {
  stopping = true;
});
async function api(path, body) {
  const r = await fetch(root + path, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Engineering-Agent-Key": key,
    },
    body: JSON.stringify(body ?? {}),
    signal: AbortSignal.timeout(10000),
  });
  if (!r.ok)
    throw new Error(r.status === 409 ? "LeaseLost" : "AgentApiUnavailable");
  return r.status === 204 ? null : await r.json();
}
let inventoryAt = 0;
async function inventory() {
  if (Date.now() - inventoryAt < 15000) return;
  const ids = (
    await command([
      "ps",
      "-aq",
      "--filter",
      "label=com.docker.compose.project=nexushr",
    ])
  )
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  if (ids.some((id) => !/^[a-f0-9]{12,64}$/.test(id)))
    throw new Error("InvalidInventory");
  const data = ids.length ? JSON.parse(await command(["inspect", ...ids])) : [];
  const containers = data.map((c) => ({
    name: c.Name.replace(/^\//, ""),
    service: c.Config.Labels["com.docker.compose.service"],
    image: c.Config.Image,
    imageId: c.Image,
    state: c.State.Status,
    health: c.State.Health?.Status ?? "none",
    restartCount: c.RestartCount,
    startedAtUtc: c.State.StartedAt,
  }));
  await api("/inventory", { containers });
  inventoryAt = Date.now();
}
function memory(s) {
  const m = /^([\d.]+)\s*(B|KiB|MiB|GiB)/.exec(s ?? "");
  return m
    ? Math.round(
        +m[1] * { B: 1, KiB: 1024, MiB: 1048576, GiB: 1073741824 }[m[2]],
      )
    : null;
}
async function infrastructure(cs, includeModel = false) {
  const id = (name) =>
    cs.find((c) => c.Config.Labels["com.docker.compose.service"] === name)?.Id;
  const data = await Promise.allSettled([
    command(
      [
        "stats",
        "--no-stream",
        "--format",
        "{{json .}}",
        id("candidate-api"),
        id("hiring-api"),
        id("gateway"),
        ...(includeModel ? [id("lab-ollama")] : []),
      ],
      10000,
    ),
    command(
      [
        "exec",
        id("candidate-db"),
        "psql",
        "-U",
        "postgres",
        "-d",
        "nexushr_candidate",
        "-Atc",
        "SELECT numbackends FROM pg_stat_database WHERE datname=current_database()",
      ],
      5000,
    ),
    command(
      [
        "exec",
        id("rabbitmq"),
        "rabbitmqctl",
        "-q",
        "list_queues",
        "messages_ready",
        "messages_unacknowledged",
        "--formatter=json",
      ],
      10000,
    ),
  ]);
  let cpuPercent = null,
    memoryBytes = null,
    databaseConnections = null,
    queueDepth = null;
  if (data[0].status === "fulfilled") {
    const samples = data[0].value
      .trim()
      .split("\n")
      .map((s) => JSON.parse(s));
    const cpu = samples.map((s) => {
      const container = cs.find((c) => c.Id.startsWith(s.ID));
      const cores =
        (container?.HostConfig.NanoCpus ?? 1_000_000_000) / 1_000_000_000;
      return parseFloat(s.CPUPerc) / Math.max(0.1, cores);
    });
    cpuPercent =
      cpu.length && cpu.every(Number.isFinite) ? Math.max(...cpu) : null;
    memoryBytes = samples.reduce((n, s) => n + (memory(s.MemUsage) ?? 0), 0);
  }
  if (data[1].status === "fulfilled" && /^\d+\s*$/.test(data[1].value))
    databaseConnections = +data[1].value;
  if (data[2].status === "fulfilled") {
    try {
      queueDepth = JSON.parse(data[2].value).reduce(
        (n, q) => n + q.messages_ready + q.messages_unacknowledged,
        0,
      );
    } catch {
      queueDepth = null;
    }
  }
  return { cpuPercent, memoryBytes, databaseConnections, queueDepth };
}
async function waitAvailable(seconds = 90) {
  const until = Date.now() + seconds * 1000;
  while (Date.now() < until) {
    if (await available()) return true;
    await sleep(1000);
  }
  return false;
}
async function execute(claim) {
  const { run, lease, recoveryRequired } = claim,
    r = run.request;
  const cs = await containers();
  let report = {
    status: "Running",
    progress: 0,
    code: "Running",
    samples: [],
    steps: [],
    checks: { isolatedLabVerified: true },
  };
  let cancelled = run.cancellationRequested,
    child = null,
    cleaned = false;
  const path = `/runs/${run.id}/${lease}`;
  const renewal = setInterval(() => {
    void api(`${path}/heartbeat`)
      .then((response) => {
        cancelled ||= response.cancellationRequested || stopping;
      })
      .catch(() => {
        cancelled = true;
      });
  }, 10000);
  async function publish() {
    await inventory();
    const response = await api(path, report);
    cancelled ||= response.cancellationRequested || stopping;
    return cancelled;
  }
  const seconds =
    r.kind === "ai-benchmark"
      ? 900
      : Math.min(
          900,
          (r.durationSeconds + 2 * r.rampSeconds + 30) *
            (r.kind === "capacity" ? r.levels.length : 1) +
            60,
        );
  const watchdog = spawn(
    process.execPath,
    [
      "scripts/engineering/watchdog.mjs",
      run.id,
      String(r.kind === "chaos" ? r.faultSeconds + 15 : seconds),
    ],
    { detached: true, windowsHide: true, stdio: "ignore" },
  );
  watchdog.unref();
  try {
    await publish();
    if (recoveryRequired) {
      report.status = "Failed";
      report.code = "InterruptedRecovered";
    } else if (r.kind === "ai-benchmark") {
      await benchmark(run, report, publish, infrastructure);
    } else if (r.kind === "chaos") {
      const ready = await available();
      if (!ready) throw new Error("InvalidLab");
      const sentinel = await createSentinel(run.id);
      const start = Date.now();
      let failures = 0,
        successes = 0,
        timeouts = 0;
      const delays = [];
      try {
        if (r.scenario === "restart-candidate")
          await compose("stop", "--timeout", "2", "candidate-api");
        else if (r.scenario === "interrupt-database")
          await proxy("/proxies/database", "POST", { enabled: false });
        else if (r.scenario === "interrupt-broker")
          await proxy("/proxies/broker", "POST", { enabled: false });
        else
          await proxy("/proxies/database/toxics", "POST", {
            name: `latency-${run.id}`,
            type: "latency",
            stream: "downstream",
            toxicity: 1,
            attributes: { latency: r.latencyMs, jitter: 0 },
          });
        if (r.scenario === "interrupt-broker")
          await verifyDuringOutage(sentinel);
        while (Date.now() - start < r.faultSeconds * 1000) {
          const observed = await probe();
          if (observed.healthy) successes++;
          else failures++;
          if (observed.timeout) timeouts++;
          delays.push(observed.latencyMs);
          const sorted = [...delays].sort((a, b) => a - b),
            p = (n) => sorted[Math.max(0, Math.ceil(sorted.length * n) - 1)];
          report.samples.push({
            elapsedSeconds: (Date.now() - start) / 1000,
            requests: successes + failures,
            failures,
            timeouts,
            rps:
              (successes + failures) / Math.max(1, (Date.now() - start) / 1000),
            averageMs: delays.reduce((a, b) => a + b, 0) / delays.length,
            p50Ms: p(0.5),
            p95Ms: p(0.95),
            p99Ms: p(0.99),
            ...(await infrastructure(cs)),
          });
          report.progress = Math.min(
            80,
            Math.floor((Date.now() - start) / (r.faultSeconds * 10)),
          );
          if (await publish()) break;
        }
      } finally {
        await recoverLab();
      }
      const recoveryStarted = Date.now();
      const recovered = await waitAvailable();
      let delivered = null;
      if (r.scenario === "interrupt-broker") {
        const deadline = Date.now() + 60000;
        while (Date.now() < deadline) {
          delivered = await messageDelivered(cs, sentinel);
          if (delivered) break;
          await publish();
          await sleep(2000);
        }
      }
      report.checks = {
        ...report.checks,
        availabilityBeforeFault: ready,
        availabilityAfterCleanup: recovered,
        failedProbeObserved: failures > 0,
        successfulProbeObserved: successes > 0,
        sentinelPreserved: await sentinelPreserved(sentinel),
      };
      if (delivered !== null) report.checks.outboxInboxRecovered = delivered;
      report.recoverySeconds = (Date.now() - recoveryStarted) / 1000;
      const allRecovered =
        recovered && report.checks.sentinelPreserved && delivered !== false;
      report.status = allRecovered
        ? cancelled
          ? "Cancelled"
          : "Succeeded"
        : "Failed";
      report.code = allRecovered
        ? cancelled
          ? "Cancelled"
          : "Completed"
        : "RecoveryFailed";
    } else {
      const levels = r.kind === "capacity" ? r.levels : [r.vus];
      for (let level = 0; level < levels.length && !cancelled; level++) {
        const vus = levels[level];
        if (
          vus < 1 ||
          vus > approvedMaxVus ||
          r.durationSeconds > 120 ||
          !["overview", "candidates", "health"].includes(r.target)
        )
          throw new Error("InvalidLab");
        const dir = resolve(directory, "runs", run.id, `step-${level}`);
        await mkdir(dir, { recursive: true });
        const name = `nexushr-engineering-k6-${run.id}-${level}`;
        const env = {
          ...process.env,
          LAB_TOKEN: await token(),
          LAB_VUS: String(vus),
          LAB_DURATION: String(r.durationSeconds),
          LAB_RAMP: String(r.rampSeconds),
          LAB_RATE: String(r.arrivalRate),
          LAB_TARGET: r.target,
          LAB_SCENARIO: r.scenario,
          LAB_RUN: run.id,
          LAB_REQUEST_BUDGET: "10000",
        };
        const args = [
          "run",
          "--rm",
          "--name",
          name,
          "--user",
          `${process.getuid?.() ?? 0}:${process.getgid?.() ?? 0}`,
          "--network",
          `${project}_default`,
          "--memory",
          "1g",
          "--cpus",
          "2",
          "--pids-limit",
          "128",
          "--cap-drop=ALL",
          "--security-opt",
          "no-new-privileges:true",
          "--read-only",
          "--tmpfs",
          "/tmp:rw,size=64m",
          "--log-driver=none",
          "--label",
          "com.docker.compose.project=nexushr-engineering-generator",
          "--label",
          "nexushr.engineering.disposable=true",
          "--label",
          `nexushr.engineering.run=${run.id}`,
          "--mount",
          `type=bind,source=${resolve("scripts/engineering")},target=/work,readonly`,
          "--mount",
          `type=bind,source=${dir},target=/results`,
        ];
        for (const key of Object.keys(env).filter((k) => k.startsWith("LAB_")))
          args.push("-e", key);
        args.push(
          image,
          "run",
          "--quiet",
          "--out",
          "json=/results/metrics.json",
          "/work/scenario.js",
        );
        child = spawn(docker, args, {
          env,
          windowsHide: true,
          stdio: "ignore",
        });
        let exit = null;
        child.once("exit", (code) => {
          exit = code ?? 1;
        });
        child.once("error", () => {
          exit = 1;
        });
        const started = Date.now();
        let offset = 0,
          pending = "",
          requests = 0,
          failures = 0,
          timeouts = 0,
          first = null,
          last = null,
          latencies = [],
          statuses = {};
        let latest = null;
        const local = [];
        while (
          exit === null ||
          ((await stat(resolve(dir, "metrics.json")).catch(() => null))?.size ??
            0) > offset
        ) {
          await sleep(1500);
          if (
            Date.now() - started >
            (r.durationSeconds + 2 * r.rampSeconds + 30) * 1000
          ) {
            await command(["stop", "--time", "2", name]).catch(() => {});
            throw new Error("TotalTimeout");
          }
          try {
            const file = resolve(dir, "metrics.json");
            const size = (await stat(file)).size;
            if (size > 64 * 1024 * 1024)
              throw new Error("RequestBudgetExceeded");
            const fd = await open(file);
            let bytes;
            try {
              const result = await fd.read({
                buffer: Buffer.alloc(Math.min(size - offset, 4_000_000)),
                position: offset,
              });
              bytes = result.buffer.subarray(0, result.bytesRead);
              offset += result.bytesRead;
            } finally {
              await fd.close();
            }
            const lines = (pending + bytes.toString()).split("\n");
            pending = lines.pop() ?? "";
            for (const line of lines) {
              if (!line) continue;
              const point = JSON.parse(line);
              if (point.type !== "Point") continue;
              const time = Date.parse(point.data.time);
              first ??= time;
              last = time;
              if (point.metric === "http_reqs") {
                requests += point.data.value;
                const status = point.data.tags?.status ?? "0";
                if (/^\d{1,3}$/.test(status))
                  statuses[status] = (statuses[status] ?? 0) + point.data.value;
              }
              if (point.metric === "http_req_failed")
                failures += point.data.value;
              if (point.metric === "http_req_duration") {
                latencies.push(point.data.value);
                if (point.data.tags?.error_code === "1050") timeouts++;
              }
            }
            if (latencies.length > 100000)
              throw new Error("RequestBudgetExceeded");
            if (requests > 0 && latencies.length > 0) {
              const sorted = [...latencies].sort((a, b) => a - b),
                elapsedSeconds = Math.max(0.001, (last - first) / 1000);
              const percentile = (p) =>
                sorted.length
                  ? sorted[Math.max(0, Math.ceil(sorted.length * p) - 1)]
                  : 0;
              latest = {
                elapsedSeconds,
                requests,
                failures,
                timeouts,
                rps: requests / elapsedSeconds,
                averageMs:
                  latencies.reduce((a, b) => a + b, 0) /
                  Math.max(1, latencies.length),
                p50Ms: percentile(0.5),
                p95Ms: percentile(0.95),
                p99Ms: percentile(0.99),
                ...(await infrastructure(cs)),
              };
              local.push(latest);
              report.samples.push(latest);
            }
          } catch (e) {
            if (e.code !== "ENOENT") throw e;
          }
          report.progress = Math.min(
            99,
            Math.floor(
              ((level +
                (Date.now() - started) /
                  ((r.durationSeconds + 2 * r.rampSeconds + 15) * 1000)) /
                levels.length) *
                100,
            ),
          );
          if (await publish()) {
            await command(["stop", "--time", "2", name]).catch(() => {});
            break;
          }
        }
        if (cancelled) break;
        if (exit !== 0 || !latest) throw new Error("ExecutionFailed");
        if (requests >= Math.floor(10000 / vus) * vus)
          throw new Error("RequestBudgetExceeded");
        const cpu = local.map((s) => s.cpuPercent).filter((x) => x !== null);
        const sustainedCpu = cpu.some(
          (_, i) =>
            i >= 2 && cpu.slice(i - 2, i + 1).every((x) => x > r.maxCpuPercent),
        );
        const failed =
          latest.failures / Math.max(1, latest.requests) > r.maxErrorRate ||
          latest.p95Ms > r.maxP95Ms ||
          sustainedCpu;
        const telemetryEnough = cpu.length >= 3;
        report.steps.push({
          vus,
          passed: !failed && telemetryEnough,
          metrics: latest,
          httpStatuses: statuses,
          outcome: !telemetryEnough
            ? "Inconclusive"
            : failed
              ? "SloBreach"
              : "Passed",
        });
        if (!telemetryEnough) {
          report.status = "Failed";
          report.code = "TelemetryInsufficient";
          break;
        }
        if (failed) {
          report.status = r.kind === "capacity" ? "Succeeded" : "Failed";
          report.code = "SloBreach";
          break;
        }
      }
      if (report.status === "Running") {
        report.status = cancelled ? "Cancelled" : "Succeeded";
        report.code = cancelled ? "Cancelled" : "Completed";
      }
    }
  } catch (e) {
    report.status = "Failed";
    report.code = [
      "TotalTimeout",
      "RequestBudgetExceeded",
      "InvalidLab",
    ].includes(e.message)
      ? e.message
      : "ExecutionFailed";
  } finally {
    // Cleanup happens before the terminal report releases the global execution slot.
    try {
      const active = (
        await command([
          "ps",
          "-q",
          "--filter",
          `label=nexushr.engineering.run=${run.id}`,
          "--filter",
          "label=com.docker.compose.project=nexushr-engineering-generator",
        ])
      )
        .trim()
        .split(/\s+/)
        .filter(Boolean);
      for (const id of active)
        if (/^[a-f0-9]{12,64}$/.test(id))
          await command(["stop", "--time", "2", id]);
      await recoverLab();
      cleaned = await waitAvailable();
      if (r.kind === "ai-benchmark")
        cleaned &&=
          report.checks.modelJobsSettled === true &&
          report.checks.modelRestored === true;
      report.checks.cleanupVerified = cleaned;
    } catch {
      report.checks.cleanupVerified = false;
    }
    if (cleaned) watchdog.kill();
    else {
      report.status = "Failed";
      report.code = "RecoveryFailed";
    }
  }
  report.progress = 100;
  clearInterval(renewal);
  await api(path, report);
  console.log(
    `Engineering run ${run.id}: ${report.status}/${report.code}. No payloads or credentials logged.`,
  );
  if (!cleaned) throw new Error("RecoveryFailed");
}
try {
  await containers();
  await recoverLab();
  while (!stopping) {
    await inventory();
    const { claim } = await api("/claim");
    if (claim) await execute(claim);
    else if (process.argv.includes("--once")) break;
    if (process.argv.includes("--once")) break;
    await sleep(2000);
  }
} catch (error) {
  const code = [
    "DockerOperationFailed",
    "InvalidLab",
    "InvalidInventory",
    "AgentApiUnavailable",
    "LeaseLost",
    "RecoveryFailed",
  ].includes(error.message)
    ? error.message
    : "OperationError";
  console.error(
    `Engineering agent stopped safely (${code}). Sensitive details withheld.`,
  );
  process.exitCode = 1;
}
