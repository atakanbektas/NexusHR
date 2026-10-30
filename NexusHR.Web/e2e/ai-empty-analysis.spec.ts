import { test, expect } from "@playwright/test";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
test("fake-provider-empty: empty input, empty response and OCR do not become useful successes", async ({ page }) => {
  test.skip(!process.env.E2E_AI_BACKEND_URL, "Owned by the isolated AiBrowserTests backend fixture.");
  const id = process.env.E2E_AI_CANDIDATE_ID!;
  const backend = process.env.E2E_AI_BACKEND_URL!;
  const kind = process.env.E2E_EMPTY_KIND;
  let bearer = "";
  page.on("request", r => { if (r.url().startsWith(`${api}/api/`) && r.headers().authorization) bearer = r.headers().authorization; });
  await page.goto("/"); await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill("hr.specialist");
  await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!");
  await page.locator("#kc-login").click(); await expect.poll(() => bearer).toMatch(/^Bearer /);
  await page.route(`${api}/api/candidates/${id}`, route => route.fulfill({ json: {
    id, firstName: "Synthetic", lastName: "Person", email: "synthetic@example.test", phoneNumber: "05555555555",
    status: "Draft", createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
  } }));
  await page.route(`${api}/api/hiring-processes/active/by-candidate/${id}`, route => route.fulfill({ json: { hasActiveProcess: false, status: null } }));
  await page.route(`${api}/api/candidates/${id}/cv-analyses**`, async route => {
    const url = new URL(route.request().url());
    const response = await route.fetch({ url: `${backend}${url.pathname}${url.search}`, headers: {
      ...route.request().headers(), "X-Ai-Test-User": "browser-hr", "X-Ai-Test-Roles": "HrSpecialist",
    } });
    await route.fulfill({ response });
  });
  await page.goto(`/candidates/${id}`); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  if (kind === "contact") {
    await expect(page.getByText(/Eski sürüm boş yanıtı başarılı kaydetmiş/)).toBeVisible();
    await expect(page.getByText("No content provided for PAGE 1", { exact: true })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Onayla", exact: true })).toHaveCount(0);
    await expect(page.getByText(/Deneme.*Succeeded/)).toHaveCount(0);
  }
  await page.getByRole("button", { name: "Analiz başlat", exact: true }).click();
  const explanation = kind === "image" ? /PDF’den okunabilir metin çıkarılamadı.*OCR/ :
    kind === "contact" ? /İletişim bilgileri gizlendikten sonra analiz edilebilir metin kalmadı/ : /Model kaynakla doğrulanabilir profesyonel bilgi üretmedi/;
  await expect(page.getByText(explanation)).toBeVisible();
  if (kind !== "image") await expect(page.getByText("Doğrulanmış sonuç yok", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Onayla", exact: true })).toHaveCount(0);
  await expect(page.getByText("Desteklenen profesyonel bilgi bulunamadı.", { exact: true })).toHaveCount(0);
  await page.reload(); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText(explanation)).toBeVisible();
});
