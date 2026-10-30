import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
test("portfolio-review: structured evidence, preview, human correction and immutable CV versions", async ({ page, request }) => {
  test.skip(!process.env.E2E_AI_BACKEND_URL, "Dedicated isolated backend fixture with deterministic provider.");
  const backend = process.env.E2E_AI_BACKEND_URL!; const id = process.env.E2E_AI_CANDIDATE_ID!;
  await page.goto("/"); await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill("hr.specialist"); await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!"); await page.locator("#kc-login").click();
  const headers = { "X-Ai-Test-User": "browser-hr", "X-Ai-Test-Roles": "HrSpecialist" };
  await page.route(`${api}/api/candidates/${id}`, route => route.fulfill({ json: {
    id, firstName: "Synthetic", lastName: "Person", email: "portfolio@example.test", phoneNumber: "05555555555", status: "DocumentsPending", createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
  } }));
  await page.route(`${api}/api/hiring-processes/active/by-candidate/${id}`, route => route.fulfill({ json: { hasActiveProcess: false } }));
  for (const suffix of ["cv-analyses**", "documents/cv**"]) await page.route(`${api}/api/candidates/${id}/${suffix}`, async route => {
    const url = new URL(route.request().url()); const response = await route.fetch({ url: `${backend}${url.pathname}${url.search}`, headers: { ...route.request().headers(), ...headers } }); await route.fulfill({ response });
  });
  await page.goto(`/candidates/${id}`); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText("Sürüm 1 (güncel) — synthetic-professional.pdf", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Analiz başlat", exact: true }).click();
  await expect(page.getByText("İş deneyimi: Engineer", { exact: true })).toBeVisible({ timeout: 45000 });
  await expect(page.getByRole("heading", { name: "İş deneyimi tablosu", exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Acme", exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "B2", exact: true })).toBeVisible();
  await expect(page.locator('object[type="application/pdf"]')).toBeVisible();
  await page.getByRole("textbox", { name: "Bulgu 1 için insan düzeltmesi", exact: true }).fill("Human-reviewed Engineer");
  await page.getByRole("button", { name: "Bulguyu düzelt: bulgu 1", exact: true }).click();
  await expect(page.getByText(/İnsan düzeltmesi: Human-reviewed Engineer/)).toBeVisible();
  const before = await (await request.get(`${backend}/api/candidates/${id}/cv-analyses`, { headers })).json(); const original = before.items[0];
  await page.locator('input[type="file"]').setInputFiles({ name: "synthetic-revision-2.pdf", mimeType: "application/pdf", buffer: Buffer.from(process.env.E2E_PORTFOLIO_PDF!, "base64") });
  await expect(page.getByText(/Sürüm 2 \(güncel\)/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Sürüm 1 önizle", exact: true })).toBeVisible();
  const after = await (await request.get(`${backend}/api/candidates/${id}/cv-analyses`, { headers })).json();
  expect(after.documentId).not.toBe(original.documentId); expect(after.items[0].id).toBe(original.id); expect(after.items[0].result.facts[0].value).toBe("Engineer");
  expect(after.items[0].reviews[0].correctedValue).toBe("Human-reviewed Engineer");
  if (process.env.CAPTURE_PORTFOLIO === "1") {
    await mkdir("../docs/screenshots", { recursive: true });
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: "../docs/screenshots/ai-review.png", fullPage: true });
  }
});
