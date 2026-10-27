import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
async function login(page: Page, user: string) {
  let authorization = "";
  page.on("request", request => {
    if (request.url().startsWith(`${api}/api/`) && request.headers().authorization) authorization = request.headers().authorization;
  });
  await page.goto("/");
  await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill(user);
  await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!");
  await page.locator("#kc-login").click();
  await expect.poll(() => authorization).toMatch(/^Bearer /);
  return { Authorization: authorization };
}

test("AI defaults off and candidate registration remains independent", async ({ page, request }) => {
  const headers = await login(page, "hr.specialist");
  const created = await request.post(`${api}/api/candidates`, { headers, data: {
    firstName: "Synthetic", lastName: "Independent", email: `ai-off-${Date.now()}@example.test`, phoneNumber: "05555555555",
  } });
  expect(created.status()).toBe(201);
  const candidate = await created.json();
  await page.goto(`/candidates/${candidate.id}`);
  await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText(/AI özelliği bu ortamda kapalıdır/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Analiz başlat", exact: true })).toBeDisabled();
  expect((await request.post(`${api}/api/candidates/${candidate.id}/cv-analyses`, {
    headers: { ...headers, "Idempotency-Key": crypto.randomUUID() }, data: { documentId: crypto.randomUUID() },
  })).status()).toBe(503);
});

test("fake-provider: real analysis pipeline, evidence and HR review in the browser", async ({ page }) => {
  test.skip(!process.env.E2E_AI_BACKEND_URL, "Executed by AiBrowserTests against its isolated PostgreSQL/RabbitMQ/MinIO fixture.");
  const backend = process.env.E2E_AI_BACKEND_URL!;
  const candidateId = process.env.E2E_AI_CANDIDATE_ID!;
  await login(page, "hr.specialist");
  // Candidate chrome is a synthetic fixture; every AI request goes to the real isolated API,
  // EF Outbox/Inbox, RabbitMQ, PDF/MinIO and deterministic provider in the test assembly.
  await page.route(`${api}/api/candidates/${candidateId}`, route => route.fulfill({ json: {
    id: candidateId, firstName: "Synthetic", lastName: "Person", email: "synthetic@example.test",
    phoneNumber: "05555555555", status: "Draft", createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
  } }));
  await page.route(`${api}/api/hiring-processes/active/by-candidate/${candidateId}`, route => route.fulfill({ json: { hasActiveProcess: false, status: null } }));
  await page.route(`${api}/api/candidates/${candidateId}/cv-analyses**`, async route => {
    const url = new URL(route.request().url());
    const response = await route.fetch({ url: `${backend}${url.pathname}${url.search}`, headers: {
      ...route.request().headers(), "X-Ai-Test-User": "browser-hr", "X-Ai-Test-Roles": "HrSpecialist",
    } });
    await route.fulfill({ response });
  });
  await page.goto(`/candidates/${candidateId}`);
  await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByRole("button", { name: "Analiz başlat", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Analiz başlat", exact: true }).click();
  await expect(page.getByText("Beceri: C#", { exact: true })).toBeVisible({ timeout: 45_000 });
  await expect(page.getByText(/Kaynak: sayfa 1, konum/)).toBeVisible();
  await page.getByRole("textbox", { name: "İnsan incelemesi / düzeltmeler" }).fill("Synthetic evidence reviewed by HR.");
  await page.getByRole("button", { name: "Onayla", exact: true }).click();
  await expect(page.getByText(/İnceleme #1: approve/)).toBeVisible();
  await page.reload();
  await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText(/İnceleme #1: approve/)).toBeVisible();
});
