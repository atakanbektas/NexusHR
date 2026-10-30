import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
test("actual scanned CV: persisted native Ollama evidence appears in HR tables", async ({ page }) => {
  test.skip(!process.env.E2E_AI_BACKEND_URL, "Explicit real OCR/Ollama fixture required; never paid CI inference.");
  const backend = process.env.E2E_AI_BACKEND_URL!; const id = process.env.E2E_AI_CANDIDATE_ID!;
  await page.goto("/"); await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill("hr.specialist"); await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!");
  await page.locator("#kc-login").click();
  await page.route(`${api}/api/candidates/${id}**`, async route => {
    const url = new URL(route.request().url());
    const response = await route.fetch({ url: `${backend}${url.pathname}${url.search}`,
      headers: { ...route.request().headers(), "X-Ai-Test-User": "browser-hr", "X-Ai-Test-Roles": "HrSpecialist" } });
    await route.fulfill({ response });
  });
  // Hiring is outside this isolated native-provider fixture; all CV/results come from its real API.
  await page.route(`${api}/api/hiring-processes/active/by-candidate/${id}`, route => route.fulfill({ json: { hasActiveProcess: false } }));
  await page.goto(`/candidates/${id}`); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText("Sağlayıcı: Yerel AI — Ollama", { exact: true })).toBeVisible();
  await expect(page.getByText(/Harici API maliyeti: 0 USD/)).toBeVisible();
  for (const name of ["İş deneyimi tablosu", "Eğitim tablosu", "Teknik beceriler tablosu"])
    await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Acme", exact: true })).toBeVisible();
  await expect(page.getByText(/Çıkarım kaynağı: tesseract/).first()).toBeVisible();
  await expect(page.locator('object[type="application/pdf"]')).toBeVisible();
  if (process.env.CAPTURE_PORTFOLIO === "1") {
    await mkdir("../docs/screenshots", { recursive: true }); await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: "../docs/screenshots/ocr-ollama.png", fullPage: true });
  }
});
