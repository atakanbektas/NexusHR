import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
const mail = process.env.E2E_MAIL_URL ?? "http://localhost:8025";

async function login(page: Page, username: string) {
  let bearer = "";
  page.on("request", request => {
    if (request.url().startsWith(api + "/api/") && request.headers().authorization)
      bearer = request.headers().authorization;
  });
  await page.goto("/");
  await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill(username);
  await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!");
  await page.locator("#kc-login").click();
  await expect.poll(() => bearer).toMatch(/^Bearer /);
  return { Authorization: bearer };
}

// A real, tiny PDF with an indexed text stream; no parser mocks or production auth bypass.
function cvPdf(email: string) {
  const text = `BT /F1 12 Tf 50 750 Td (Ada Lovelace) Tj 0 -20 Td (${email}) Tj 0 -20 Td (05555555555) Tj ET`;
  const objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", `<< /Length ${text.length} >>\nstream\n${text}\nendstream`];
  let pdf = "%PDF-1.4\n";
  const offsets = [0];
  objects.forEach((object, i) => { offsets.push(pdf.length); pdf += `${i + 1} 0 obj\n${object}\nendobj\n`; });
  const xref = pdf.length;
  pdf += `xref\n0 6\n0000000000 65535 f \n${offsets.slice(1).map(offset => `${offset.toString().padStart(10, "0")} 00000 n \n`).join("")}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`;
  return Buffer.from(pdf);
}

test("candidate, CV extraction, offer email/acceptance and onboarding completion", async ({ page, browser, request }) => {
  const headers = await login(page, "hr.specialist");
  const email = `e2e-${Date.now()}@example.test`;
  await page.goto("/candidates/new");
  await page.locator('input[type="file"]').setInputFiles({ name: "cv.pdf", mimeType: "application/pdf", buffer: cvPdf(email) });
  await page.getByRole("button", { name: /CV.*analiz|Bilgileri.*çıkar|Analiz/i }).click();
  await expect(page.getByRole("textbox", { name: "E-posta", exact: true })).toHaveValue(email);
  await page.getByRole("textbox", { name: "Ad", exact: true }).fill("Ada");
  await page.getByRole("textbox", { name: "Soyad", exact: true }).fill("Lovelace");
  await page.getByRole("textbox", { name: "Telefon", exact: true }).fill("05555555555");
  const created = page.waitForResponse(response => response.url() === `${api}/api/candidates` && response.request().method() === "POST");
  await page.getByRole("button", { name: "Taslak adayı kaydet" }).click();
  const createdResponse = await created;
  expect(createdResponse.status()).toBe(201);
  const candidate = await createdResponse.json();
  await expect(page.getByText(/Taslak aday ve CV başarıyla kaydedildi/)).toBeVisible();
  const download = await request.get(`${api}/api/candidates/${candidate.id}/documents/cv`, { headers });
  expect(download.status()).toBe(200);
  expect((await download.body()).subarray(0, 5).toString()).toBe("%PDF-");
  expect((await request.post(`${api}/api/candidates/${candidate.id}/documents/verify`, { headers })).status()).toBe(204);

  let hiringId = "";
  await expect.poll(async () => {
    const response = await request.post(`${api}/api/hiring-processes`, { headers, data: {
      candidateId: candidate.id, positionTitle: "Software Engineer", department: "Engineering", employmentType: 1,
    } });
    if (response.status() === 201) hiringId = (await response.json()).id;
    return response.status();
  }, { timeout: 45_000, intervals: [500, 1000, 2000] }).toBe(201);
  expect((await request.put(`${api}/api/hiring-processes/${hiringId}/offer`, { headers, data: {
    grossSalary: 100000, currency: "TRY", proposedStartDate: new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10),
    offerExpiresAtUtc: new Date(Date.now() + 7 * 86400000).toISOString(),
  } })).status()).toBe(204);
  expect((await request.post(`${api}/api/hiring-processes/${hiringId}/offer/send`, { headers })).status()).toBe(204);

  let offerUrl = "";
  await expect.poll(async () => {
    const response = await request.get(`${mail}/api/v2/search?kind=to&query=${encodeURIComponent(email)}`);
    const messages = await response.json();
    const body = String(messages.items?.[0]?.Content?.Body ?? "").replace(/=\r?\n/g, "")
      .replace(/=([0-9a-f]{2})/gi, (_, hex) => String.fromCharCode(parseInt(hex, 16)));
    offerUrl = body.match(/http[^"\s<>]+\/public\/offers\/respond\?token=[^"\s<>]+/)?.[0]?.replaceAll("&amp;", "&") ?? "";
    return Boolean(offerUrl);
  }, { timeout: 45_000 }).toBe(true);
  const anonymous = await browser.newContext();
  const offerPage = await anonymous.newPage();
  await offerPage.goto(offerUrl);
  offerPage.on("dialog", dialog => dialog.accept());
  await offerPage.getByRole("button", { name: "Teklifi kabul et" }).click();
  await expect(offerPage.getByText(/İş teklifini kabul ettiğiniz/)).toBeVisible();
  await anonymous.close();

  let onboardingId = "";
  await expect.poll(async () => {
    const response = await request.get(`${api}/api/onboardings`, { headers });
    const rows = await response.json();
    const row = rows.find((item: { hiringProcessId: string }) => item.hiringProcessId === hiringId);
    onboardingId = row?.id ?? "";
    return rows.filter((item: { hiringProcessId: string }) => item.hiringProcessId === hiringId).length;
  }, { timeout: 45_000 }).toBe(1);
  await page.goto(`/onboardings/${onboardingId}`);
  for (let completed = 1; completed <= 3; completed++) {
    await page.getByRole("button", { name: "Başlat", exact: true }).first().click();
    await page.getByRole("button", { name: "Görevi tamamla", exact: true }).first().click();
    await expect(page.getByText(`${completed} / 3 görev tamamlandı`, { exact: true })).toBeVisible();
  }
  await page.getByRole("button", { name: "Onboarding tamamla", exact: true }).click();
  await expect.poll(async () => (await (await request.get(`${api}/api/onboardings/${onboardingId}`, { headers })).json()).status).toBe("Completed");
  await expect.poll(async () => (await (await request.get(`${api}/api/hiring-processes/${hiringId}`, { headers })).json()).status,
    { timeout: 45_000 }).toBe("Completed");
  expect((await request.get(`${api}/api/onboardings`)).status()).toBe(401);
});

test("auditor can read but cannot upload a CV", async ({ page, request }) => {
  const headers = await login(page, "auditor");
  expect((await request.get(`${api}/api/candidates`, { headers })).status()).toBe(200);
  const response = await request.post(`${api}/api/candidates/11111111-1111-1111-1111-111111111111/documents/cv`, {
    headers, multipart: { file: { name: "cv.pdf", mimeType: "application/pdf", buffer: cvPdf("auditor@example.test") } },
  });
  expect(response.status()).toBe(403);
});
