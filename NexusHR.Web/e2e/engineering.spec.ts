import {
  test,
  expect,
  type Page,
  type APIRequestContext,
} from "@playwright/test";
import { readFile, mkdir } from "node:fs/promises";
import { resolve } from "node:path";
const api = "http://localhost:5000/api/engineering";
type Report = {
  code: string;
  samples: { requests: number; p95Ms: number }[];
  steps: { vus: number; passed: boolean; outcome: string }[];
  checks: Record<string, boolean>;
  benchmarks?: {
    model: string;
    status: string;
    validatedFacts: number | null;
    goldMarkersMatched: number | null;
    goldMarkersTotal: number;
    nativeOutputTokensPerSecond: number | null;
  }[];
};
type Run = { id: string; status: string; report: Report };
test.skip(
  process.env.RUN_ENGINEERING_TESTS !== "1",
  "Explicit local engineering stack and generated technical accounts required. No load in ordinary CI.",
);
async function login(page: Page, role: "reader" | "operator") {
  const credentials = JSON.parse(
    await readFile(resolve("../.tools/engineering/access.json"), "utf8"),
  );
  let authorization = "";
  page.on("request", (r) => {
    if (r.url().startsWith(api) && r.headers().authorization)
      authorization = r.headers().authorization;
  });
  await page.goto("/");
  await page
    .getByRole("button", { name: "Personel girişi", exact: true })
    .click();
  await page.locator("#username").fill(`engineering.${role}`);
  await page.locator("#password").fill(credentials[role]);
  await page.locator("#kc-login").click();
  await expect(
    page.getByRole("heading", { name: "Control Center", exact: true }),
  ).toBeVisible();
  await expect.poll(() => authorization).toMatch(/^Bearer /);
  return { Authorization: authorization };
}
async function terminal(
  request: APIRequestContext,
  id: string,
  headers: Record<string, string>,
  seconds = 150,
) {
  let run: Run | undefined;
  await expect
    .poll(
      async () => {
        const r = await request.get(`${api}/runs/${id}`, { headers });
        expect(r.ok()).toBe(true);
        run = await r.json();
        return run!.status;
      },
      { timeout: seconds * 1000, intervals: [1500, 2500] },
    )
    .toMatch(/Succeeded|Failed|Cancelled/);
  return run!;
}
const experiment = {
  kind: "load",
  target: "overview",
  scenario: "constant",
  vus: 5,
  durationSeconds: 15,
  rampSeconds: 0,
  arrivalRate: 10,
  maxErrorRate: 0.01,
  maxP95Ms: 1000,
  maxCpuPercent: 100,
  confirmation: "SYNTHETIC LAB ONLY",
  faultSeconds: 3,
  latencyMs: 100,
};
async function create(
  request: APIRequestContext,
  headers: Record<string, string>,
  body: object,
) {
  const r = await request.post(`${api}/runs`, {
    headers: { ...headers, "Idempotency-Key": crypto.randomUUID() },
    data: body,
  });
  expect(r.status()).toBe(202);
  return (await r.json()).id as string;
}

test("technical reader sees real sources without receiving HR/CV or execution permissions", async ({
  page,
  request,
}) => {
  const headers = await login(page, "reader");
  const overview = await request.get(`${api}/overview`, { headers });
  expect(overview.ok()).toBe(true);
  const health = await overview.json();
  expect(health.services).toHaveLength(5);
  expect(health.services.every((s: { healthy: boolean }) => s.healthy)).toBe(
    true,
  );
  for (const source of ["broker", "databases/candidate", "ai"]) {
    const r = await request.get(`${api}/${source}`, { headers });
    expect(r.ok()).toBe(true);
    expect((await r.json()).available).toBe(true);
  }
  const memory = await request.get(`${api}/metrics/memory`, { headers });
  const measured = await memory.json();
  expect(measured.available).toBe(true);
  expect(measured.data.length).toBeGreaterThan(0);
  expect(
    measured.data.some((s: { values: [number, number][] }) =>
      s.values.some((v) => v[1] > 0),
    ),
  ).toBe(true);
  expect(
    (
      await request.get(
        "http://localhost:5000/api/candidates?page=1&pageSize=1",
        { headers },
      )
    ).status(),
  ).toBe(403);
  expect(
    (await request.post(`${api}/runs`, { headers, data: experiment })).status(),
  ).toBe(403);
  expect(
    (
      await request.post("http://localhost:5000/api/engineering-agent/claim", {
        headers,
      })
    ).status(),
  ).toBe(401);
  await page
    .getByRole("navigation", { name: "Engineering navigation" })
    .getByRole("link", { name: "Load Testing", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Start experiment", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("navigation", { name: "Engineering navigation" })
    .getByRole("link", { name: "Overview", exact: true })
    .click();
  if (process.env.CAPTURE_ENGINEERING === "1") {
    await expect(page).toHaveURL(/\/engineering\/overview$/);
    await page.mouse.move(1200, 20);
    await page.waitForTimeout(250);
    await mkdir(resolve("../docs/screenshots"), { recursive: true });
    await page.screenshot({
      path: resolve("../docs/screenshots/engineering-overview.png"),
      fullPage: true,
    });
  }
});

test("cached local models are benchmarked on the fixed synthetic PDF without changing main AI", async ({
  page,
  request,
}) => {
  test.skip(
    process.env.RUN_ENGINEERING_BENCHMARK !== "1",
    "Explicit expensive local-model benchmark approval required.",
  );
  test.setTimeout(900000);
  const headers = await login(page, "operator");
  const id = await create(request, headers, {
    ...experiment,
    kind: "ai-benchmark",
    target: "candidates",
    vus: 1,
    models: ["qwen3:4b", "qwen2.5vl:3b"],
  });
  const result = await terminal(request, id, headers, 850);
  expect(result.report.checks.cleanupVerified).toBe(true);
  expect(result.report.benchmarks).toHaveLength(2);
  const baseline = result.report.benchmarks!.find(
    (x) => x.model === "qwen3:4b",
  )!;
  expect(baseline.status).toBe("Succeeded");
  expect(baseline.validatedFacts).toBeGreaterThan(0);
  expect(baseline.goldMarkersTotal).toBe(4);
  expect(baseline.nativeOutputTokensPerSecond).toBeGreaterThan(0);
  // An alternate model's measured failure is retained, not relabeled as accurate/successful.
  await page
    .getByRole("navigation", { name: "Engineering navigation" })
    .getByRole("link", { name: "AI Observability", exact: true })
    .click();
  await expect(page.getByText("bilingual-v1").first()).toBeVisible();
  if (process.env.CAPTURE_ENGINEERING === "1")
    await page.screenshot({
      path: resolve("../docs/screenshots/engineering-ai.png"),
      fullPage: true,
    });
});

test("operator starts real bounded k6 from the UI, exports measured results and cancels a run", async ({
  page,
  request,
}) => {
  test.skip(
    process.env.RUN_ENGINEERING_EXECUTION !== "1",
    "Explicit low-load execution approval required.",
  );
  test.setTimeout(300000);
  const headers = await login(page, "operator");
  await expect
    .poll(
      async () => {
        const r = await request.get(`${api}/capabilities`, { headers });
        return (await r.json()).agentOnline;
      },
      { timeout: 30000 },
    )
    .toBe(true);
  await page
    .getByRole("navigation", { name: "Engineering navigation" })
    .getByRole("link", { name: "Load Testing", exact: true })
    .click();
  await page
    .getByRole("checkbox", {
      name: "I approve this experiment in the disposable SYNTHETIC LAB ONLY",
    })
    .check();
  const queued = page.waitForResponse(
    (r) => r.url() === `${api}/runs` && r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Start experiment", exact: true })
    .click();
  const response = await queued;
  expect(response.status()).toBe(202);
  const { id } = await response.json();
  const completed = await terminal(request, id, headers);
  expect(completed.status).toBe("Succeeded");
  expect(completed.report.samples.at(-1)!.requests).toBeGreaterThan(0);
  expect(completed.report.samples.at(-1)!.p95Ms).toBeGreaterThan(0);
  expect(completed.report.checks.cleanupVerified).toBe(true);
  await expect(
    page.getByText("Selected experiment · Succeeded / Completed"),
  ).toBeVisible();
  for (const format of ["json", "csv"]) {
    const download = page.waitForEvent("download");
    await page
      .getByRole("button", {
        name: `Export ${format.toUpperCase()}`,
        exact: true,
      })
      .click();
    expect((await download).suggestedFilename()).toBe(
      `engineering-${id}.${format}`,
    );
  }
  const cancelledId = await create(request, headers, {
    ...experiment,
    durationSeconds: 60,
  });
  await expect
    .poll(
      async () => {
        const r = await request.get(`${api}/runs/${cancelledId}`, { headers });
        return (await r.json()).status;
      },
      { timeout: 30000 },
    )
    .toBe("Running");
  expect(
    (
      await request.post(`${api}/runs/${cancelledId}/cancel`, { headers })
    ).status(),
  ).toBe(202);
  const cancelled = await terminal(request, cancelledId, headers);
  expect(cancelled.status).toBe("Cancelled");
  expect(cancelled.report.checks.cleanupVerified).toBe(true);
  const denied = await request.post(`${api}/runs`, {
    headers: { ...headers, "Idempotency-Key": crypto.randomUUID() },
    data: { ...experiment, target: "https://production.invalid" },
  });
  expect(denied.status()).toBe(400);
  if (process.env.CAPTURE_ENGINEERING === "1") {
    await page.screenshot({
      path: resolve("../docs/screenshots/engineering-load.png"),
      fullPage: true,
    });
    await page.pdf({
      path: resolve("../.tools/engineering/performance-report.pdf"),
      format: "A4",
      printBackground: true,
    });
  }
});

test("capacity exploration records an observed SLO breach without declaring absolute capacity", async ({
  page,
  request,
}) => {
  test.skip(
    process.env.RUN_ENGINEERING_EXECUTION !== "1",
    "Explicit isolated performance approval required.",
  );
  test.setTimeout(180000);
  const headers = await login(page, "operator");
  const id = await create(request, headers, {
    ...experiment,
    kind: "capacity",
    levels: [1, 2],
    maxP95Ms: 1,
  });
  const run = await terminal(request, id, headers);
  expect(run.status).toBe("Succeeded");
  expect(run.report.code).toBe("SloBreach");
  expect(run.report.steps[0].vus).toBe(1);
  expect(run.report.steps[0].passed).toBe(false);
  expect(run.report.steps[0].outcome).toBe("SloBreach");
});

for (const scenario of [
  "restart-candidate",
  "interrupt-broker",
  "interrupt-database",
  "latency",
])
  test(`real isolated fault ${scenario} recovers and preserves a synthetic sentinel`, async ({
    page,
    request,
  }) => {
    test.skip(
      process.env.RUN_ENGINEERING_EXECUTION !== "1",
      "Explicit isolated failure-test approval required.",
    );
    test.setTimeout(240000);
    const headers = await login(page, "operator");
    const id = await create(request, headers, {
      ...experiment,
      kind: "chaos",
      scenario,
      faultSeconds: 3,
    });
    const run = await terminal(request, id, headers, 210);
    expect(run.status).toBe("Succeeded");
    expect(run.report.checks.sentinelPreserved).toBe(true);
    expect(run.report.checks.cleanupVerified).toBe(true);
    if (scenario === "interrupt-broker")
      expect(run.report.checks.outboxInboxRecovered).toBe(true);
  });
