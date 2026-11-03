import { randomUUID } from "node:crypto";
import {
  configureModel,
  containers,
  labRequest,
  sleep,
  command,
} from "./lab.mjs";

const golden = [
  ["skill", "C#"],
  ["skill", "PostgreSQL"],
  ["employment", "Engineer"],
  ["education", "Example University"],
];
// Fixed, public synthetic input. The browser/PdfPig path preserves Turkish Unicode.
const html = `<!doctype html><html lang="tr"><meta charset="utf-8"><style>body{font-family:Arial;font-size:14px;line-height:1.7;margin:48px}</style>
<h1>Synthetic Benchmark Person</h1><p>Tamamen sentetik test özgeçmişi. Gerçek kişi değildir.</p>
<p>Profesyonel özet / Professional summary: Kurumsal web uygulamaları geliştiren yazılım geliştirici.</p>
<p>Teknik beceriler / Technical skills: C#, PostgreSQL, Docker.</p>
<p>İş deneyimi / Employment: Synthetic Labs | Engineer | 2021-01 | 2024-12.</p>
<p>Eğitim / Education: Example University | Computer Science | BSc | 2016-2020.</p>
<p>Diller / Languages: English B2, Türkçe C2.</p></html>`;

async function ready() {
  const until = Date.now() + 90000;
  while (Date.now() < until) {
    try {
      await labRequest("?page=1&pageSize=1");
      return;
    } catch {}
    await sleep(1000);
  }
  throw new Error("LabNotReady");
}
// Cancellation must reach a durable terminal state before releasing the experiment slot.
export async function settleAnalysis(
  path,
  id,
  request = labRequest,
  wait = sleep,
) {
  let value = await request(`${path}/${id}`);
  if (!["Queued", "Running"].includes(value.status)) return;
  await request(`${path}/${id}/cancel`, {
    method: "POST",
    headers: { "If-Match": `"${value.version}"` },
  });
  for (let attempt = 0; attempt < 30; attempt++) {
    value = await request(`${path}/${id}`);
    if (!["Queued", "Running"].includes(value.status)) return;
    await wait(1000);
  }
  throw new Error("ModelJobNotSettled");
}
export async function benchmark(run, report, publish, infrastructure) {
  const { chromium } =
    await import("../../NexusHR.Web/node_modules/@playwright/test/index.mjs");
  const browser = await chromium.launch({ headless: true });
  let pdf;
  try {
    const page = await browser.newPage();
    await page.setContent(html);
    pdf = await page.pdf({ format: "A4" });
  } finally {
    await browser.close();
  }
  report.benchmarks = [];
  let phase = "configure";
  let pending = null;
  report.checks.modelJobsSettled = false;
  report.checks.modelRestored = false;
  try {
    for (const model of run.request.models) {
      if (await publish()) break;
      await configureModel(model);
      phase = "readiness";
      await ready();
      phase = "create";
      const cs = await containers();
      const created = await labRequest("", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          firstName: "Synthetic",
          lastName: "Benchmark",
          email: `benchmark-${run.id}-${model.replace(/[^a-z0-9]/g, "")}@example.test`,
          phoneNumber: "05555555555",
        }),
      });
      if (!/^[a-f0-9-]{36}$/.test(created.id)) throw new Error("InvalidLab");
      const base = `/${created.id}/cv-analyses`;
      const upload = new FormData();
      upload.append(
        "file",
        new Blob([pdf], { type: "application/pdf" }),
        "synthetic-benchmark.pdf",
      );
      await labRequest(`/${created.id}/documents/cv`, {
        method: "POST",
        body: upload,
      });
      phase = "request";
      const history = await labRequest(base);
      const start = Date.now();
      if (!history.settings.ready) {
        report.benchmarks.push({
          model,
          dataset: "bilingual-v1",
          status: "ModelNotReady",
          errorCode: history.settings.unavailableCode,
          durationSeconds: 0,
          validatedFacts: null,
          goldMarkersMatched: null,
          goldMarkersTotal: 4,
          nativeOutputTokensPerSecond: null,
          peakCpuPercent: null,
          peakMemoryBytes: null,
          modelDigest: null,
        });
        continue;
      }
      let analysis = await labRequest(base, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": randomUUID(),
        },
        body: JSON.stringify({ documentId: history.documentId }),
      });
      pending = { base, id: analysis.id };
      report.checks.modelJobCreated = true;
      await publish();
      let peakCpu = null,
        peakMemory = null,
        cancelled = false;
      phase = "poll";
      while (
        ["Queued", "Running"].includes(analysis.status) &&
        Date.now() - start < 420000
      ) {
        const snapshot = await infrastructure(cs, true);
        if (snapshot.cpuPercent !== null)
          peakCpu = Math.max(peakCpu ?? 0, snapshot.cpuPercent);
        if (snapshot.memoryBytes !== null)
          peakMemory = Math.max(peakMemory ?? 0, snapshot.memoryBytes);
        analysis = await labRequest(`${base}/${analysis.id}`);
        cancelled = await publish();
        if (cancelled && ["Queued", "Running"].includes(analysis.status)) {
          await labRequest(`${base}/${analysis.id}/cancel`, {
            method: "POST",
            headers: { "If-Match": `"${analysis.version}"` },
          });
          const until = Date.now() + 30000;
          while (
            ["Queued", "Running"].includes(analysis.status) &&
            Date.now() < until
          ) {
            await sleep(1000);
            analysis = await labRequest(`${base}/${analysis.id}`);
          }
          break;
        }
        await sleep(1500);
      }
      if (["Queued", "Running"].includes(analysis.status)) {
        await labRequest(`${base}/${analysis.id}/cancel`, {
          method: "POST",
          headers: { "If-Match": `"${analysis.version}"` },
        });
        throw new Error("BenchmarkTimeout");
      }
      const facts = analysis.result?.facts;
      pending = null;
      phase = "metadata";
      const matched = facts
        ? golden.filter(([category, marker]) =>
            facts.some(
              (f) =>
                f.category === category &&
                [f.value, ...(f.fields ?? []).map((p) => p.value)].some(
                  (value) =>
                    value
                      .toLocaleLowerCase("en")
                      .includes(marker.toLocaleLowerCase("en")),
                ),
            ),
          ).length
        : null;
      if (!/^[a-f0-9-]{36}$/.test(analysis.id)) throw new Error("InvalidLab");
      const db = cs.find(
        (c) => c.Config.Labels["com.docker.compose.service"] === "candidate-db",
      );
      const row = await command([
        "exec",
        db.Id,
        "psql",
        "-U",
        "postgres",
        "-d",
        "nexushr_candidate",
        "-Atc",
        `SELECT json_build_object('duration',EXTRACT(EPOCH FROM ("FinishedAtUtc"-"StartedAtUtc")),'native',"NativeOutputTokensPerSecond",'digest',"ProviderRequestId") FROM cv_analysis_attempts WHERE "AnalysisId"='${analysis.id}'::uuid ORDER BY "Number" DESC LIMIT 1`,
      ]);
      const meta = row.trim() ? JSON.parse(row) : {};
      const digest = (meta.digest ?? "").replace(/^ollama:(sha256:)?/, "");
      report.benchmarks.push({
        model,
        dataset: "bilingual-v1",
        status: analysis.status,
        errorCode: analysis.errorCode,
        durationSeconds: meta.duration ?? (Date.now() - start) / 1000,
        validatedFacts: facts?.length ?? null,
        goldMarkersMatched: matched,
        goldMarkersTotal: 4,
        nativeOutputTokensPerSecond: meta.native ?? null,
        peakCpuPercent: peakCpu,
        peakMemoryBytes: peakMemory,
        modelDigest: /^[a-f0-9]{12,64}$/.test(digest) ? digest : null,
      });
      report.progress = Math.floor(
        (report.benchmarks.length / run.request.models.length) * 95,
      );
      if (cancelled) break;
    }
  } catch (error) {
    // Fixed stage and exception type only: never print response bodies, prompts or credentials.
    const status = Number.isInteger(error.status) ? error.status : "none";
    console.error(
      `Synthetic benchmark failed at ${phase} (${error instanceof TypeError ? "TypeError" : "OperationError"}; HTTP ${status}).`,
    );
    throw error;
  } finally {
    try {
      if (pending) await settleAnalysis(pending.base, pending.id);
      report.checks.modelJobsSettled = true;
    } finally {
      await configureModel("qwen3:4b");
      await ready();
      report.checks.modelRestored = true;
    }
  }
  const cancelled = await publish();
  report.status = cancelled
    ? "Cancelled"
    : report.benchmarks.every((b) => b.status === "Succeeded")
      ? "Succeeded"
      : "Failed";
  report.code = cancelled
    ? "Cancelled"
    : report.status === "Succeeded"
      ? "Completed"
      : "ExecutionFailed";
}
