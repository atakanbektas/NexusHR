import { test, expect } from "@playwright/test";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve } from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
test("real Ollama: synthetic Turkish PDF upload, local analysis, evidence and persistent HR review", async ({ page, browser, request }) => {
  test.skip(process.env.RUN_OLLAMA_TESTS !== "1", "Opt-in only: requires the ai Docker profile and downloaded local model.");
  test.setTimeout(420_000);
  let authorization = "";
  page.on("request", r => { if (r.url().startsWith(`${api}/api/`) && r.headers().authorization) authorization = r.headers().authorization; });
  await page.goto("/"); await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill("hr.specialist");
  await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!");
  await page.locator("#kc-login").click(); await expect.poll(() => authorization).toMatch(/^Bearer /);
  const email = `ollama-synthetic-${Date.now()}@example.test`;
  const pdfPage = await browser.newPage();
  await pdfPage.setContent((await readFile(resolve("e2e/fixtures/synthetic-developer-tr.html"), "utf8")).replace("synthetic@example.test", email));
  const pdf = await pdfPage.pdf({ format: "A4", printBackground: true }); await pdfPage.close();
  await page.goto("/candidates/new");
  await page.locator('input[type="file"]').setInputFiles({ name: "synthetic-developer-tr.pdf", mimeType: "application/pdf", buffer: pdf });
  await page.getByRole("button", { name: /CV.*analiz|Bilgileri.*çıkar|Analiz/i }).click();
  await expect(page.getByRole("textbox", { name: "E-posta", exact: true })).toHaveValue(email);
  await page.getByRole("textbox", { name: "Ad", exact: true }).fill("Deniz");
  await page.getByRole("textbox", { name: "Soyad", exact: true }).fill("Örnek");
  await page.getByRole("textbox", { name: "Telefon", exact: true }).fill("05555555555");
  const created = page.waitForResponse(r => r.url() === `${api}/api/candidates` && r.request().method() === "POST");
  await page.getByRole("button", { name: "Taslak adayı kaydet" }).click();
  const response = await created; expect(response.status()).toBe(201); const candidate = await response.json();
  await expect(page.getByText(/Taslak aday ve CV başarıyla kaydedildi/)).toBeVisible();
  await page.goto(`/candidates/${candidate.id}`); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText("Sağlayıcı: Yerel AI — Ollama", { exact: true })).toBeVisible();
  await expect(page.getByText(/Harici API maliyeti: 0 USD/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Analiz başlat", exact: true })).toBeEnabled();
  const started = page.waitForResponse(r => r.url() === `${api}/api/candidates/${candidate.id}/cv-analyses` && r.request().method() === "POST");
  await page.getByRole("button", { name: "Analiz başlat", exact: true }).click();
  const startedResponse = await started; expect(startedResponse.status()).toBe(202); const run = await startedResponse.json();
  const headers = { Authorization: authorization };
  let final: { status: string; provider: string; result: { facts: { category: string; value: string; page: number; start: number; quote: string }[] }; attempts: { usageCostUsd: number }[] } | undefined;
  await expect.poll(async () => {
    const status = await request.get(`${api}/api/candidates/${candidate.id}/cv-analyses/${run.id}`, { headers }); final = await status.json();
    return final!.status;
  }, { timeout: 300_000, intervals: [2000, 3000] }).toBe("Succeeded");
  expect(final!.provider).toBe("Ollama"); expect(final!.attempts.every(a => a.usageCostUsd === 0)).toBe(true);
  for (const category of ["summary", "skill", "employment", "education", "certification", "language"])
    expect(final!.result.facts.some(f => f.category === category), category).toBe(true);
  expect(final!.result.facts.some(f => f.value.includes("C#"))).toBe(true);
  expect(final!.result.facts.every(f => f.page === 1 && f.start >= 0 && f.quote.includes(f.value))).toBe(true);
  await expect(page.getByText(/Kaynak: sayfa 1, konum/).first()).toBeVisible();
  await page.getByRole("textbox", { name: "İnsan incelemesi / düzeltmeler" }).fill("Sentetik yerel model sonucu kaynaklarıyla incelendi.");
  await page.getByRole("button", { name: "Onayla", exact: true }).click(); await expect(page.getByText(/İnceleme #1: approve/)).toBeVisible();
  await page.reload(); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText(/İnceleme #1: approve/)).toBeVisible();
  const history = await request.get(`${api}/api/candidates/${candidate.id}/cv-analyses`, { headers });
  expect((await history.json()).items.some((r: { id: string }) => r.id === run.id)).toBe(true);
  const docker = process.env.DOCKER_EXE ?? "docker";
  const exec = promisify(execFile);
  expect(run.id).toMatch(/^[a-f0-9-]{36}$/i);
  const persisted = await exec(docker, ["exec", "nexushr-candidate-db", "psql", "-U", "postgres", "-d", "nexushr_candidate", "-Atc",
    `SELECT a."Provider",a."Status",a."ReservedUsd",t."UsageCostUsd",r."Decision" FROM cv_analyses a JOIN cv_analysis_attempts t ON t."AnalysisId"=a."Id" JOIN cv_analysis_reviews r ON r."AnalysisId"=a."Id" WHERE a."Id"='${run.id}';`]);
  expect(persisted.stdout).toContain("Ollama|Succeeded|0.00000000|0.00000000|approve");
  const local = await exec(docker, ["compose", "-f", "../docker-compose.yml", "-f", "../docker-compose.ai.yml", "--profile", "ai", "ps", "-q", "ollama"]);
  const ollamaId = local.stdout.trim(); expect(ollamaId).toMatch(/^[a-f0-9]{12,64}$/);
  await exec(docker, ["restart", ollamaId]);
  await exec(docker, ["restart", "nexushr-candidate-api"]);
  await expect.poll(async () => {
    const response = await request.get(`${api}/api/candidates/${candidate.id}/cv-analyses`, { headers });
    return response.ok() && (await response.json()).settings.ready;
  }, { timeout: 60_000, intervals: [1000, 2000] }).toBe(true);
  const restored = await request.get(`${api}/api/candidates/${candidate.id}/cv-analyses/${run.id}`, { headers });
  const restoredResult = await restored.json(); expect(restoredResult.status).toBe("Succeeded"); expect(restoredResult.reviews[0].decision).toBe("approve");
  // Synthetic artefact only; ignored test-results, never commit CV content or result data.
  await mkdir("test-results", { recursive: true });
  await writeFile("test-results/ollama-real-result.json", JSON.stringify({ candidateId: candidate.id, analysisId: run.id, ...restoredResult, databaseProof: persisted.stdout.trim(), restartVerified: true }, null, 2));
});
