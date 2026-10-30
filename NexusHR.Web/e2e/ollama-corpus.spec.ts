import { test, expect, type Page } from "@playwright/test";
import { mkdir, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { resolve } from "node:path";

const api = process.env.E2E_API_URL ?? "http://localhost:5000";
const cases = [
  { id: "tr-backend", lang: "tr", role: "Backend Yazılım Geliştirici", skills: "C#, ASP.NET Core, PostgreSQL, RabbitMQ", company: "Sentetik Finans Teknolojileri", work: "Ödeme API servisleri geliştirdi. Birim ve entegrasyon testleri yazdı.", school: "Örnek Teknik Üniversitesi, Bilgisayar Mühendisliği", years: "2020-2024" },
  { id: "tr-frontend", lang: "tr", role: "Frontend Geliştirici", skills: "React, TypeScript, JavaScript, HTML, CSS", company: "Sentetik Web Çözümleri", work: "Erişilebilir yönetim ekranları ve test edilmiş form bileşenleri geliştirdi.", school: "Örnek Üniversitesi, Yazılım Mühendisliği", years: "2021-2025" },
  { id: "tr-data", lang: "tr", role: "Veri Mühendisi", skills: "Python, SQL, PostgreSQL, Airflow", company: "Sentetik Veri Sistemleri", work: "Veri aktarım işlerini ve raporlama sorgularını yönetti.", school: "Örnek Üniversitesi, Matematik", years: "2019-2023" },
  { id: "tr-devops", lang: "tr", role: "DevOps Mühendisi", skills: "Docker, Kubernetes, Terraform, Linux, Azure", company: "Sentetik Bulut Platformu", work: "CI/CD akışlarını kurdu. Dağıtım ve izleme altyapısını yönetti.", school: "Örnek Teknik Üniversitesi, Bilgisayar Mühendisliği", years: "2020-2025" },
  { id: "tr-qa", lang: "tr", role: "Test Otomasyon Mühendisi", skills: "C#, xUnit, Playwright, SQL, Git", company: "Sentetik Kalite Yazılım", work: "API ve tarayıcı regresyon testleri geliştirdi. Hataların tekrarını doğruladı.", school: "Örnek Üniversitesi, Yazılım Mühendisliği", years: "2022-2025" },
  { id: "en-fullstack", lang: "en", role: "Full Stack Software Engineer", skills: "C#, ASP.NET Core, React, TypeScript, PostgreSQL", company: "Synthetic Enterprise Systems", work: "Built onboarding APIs and accessible HR dashboards with automated regression tests.", school: "Example Technical University, Computer Engineering", years: "2020-2024" },
  { id: "en-backend", lang: "en", role: "Backend Engineer", skills: "Java, Spring Boot, PostgreSQL, RabbitMQ, Docker", company: "Synthetic Commerce Labs", work: "Developed order processing services with transactional messaging and integration tests.", school: "Example University, Computer Science", years: "2019-2023" },
  { id: "en-cloud", lang: "en", role: "Cloud Platform Engineer", skills: "Linux, Kubernetes, Terraform, AWS, Python", company: "Synthetic Platform Tools", work: "Maintained deployment pipelines and monitored service availability.", school: "Example Institute, Software Engineering", years: "2021-2025" },
  { id: "en-data", lang: "en", role: "Data Engineer", skills: "Python, SQL, Apache Spark, Airflow, PostgreSQL", company: "Synthetic Analytics Group", work: "Built data validation pipelines and maintained reporting datasets.", school: "Example University, Mathematics", years: "2020-2023" },
  { id: "en-quality", lang: "en", role: "Quality Automation Engineer", skills: "TypeScript, Playwright, REST, Git, SQL", company: "Synthetic Quality Studio", work: "Created browser and API security regression tests for enterprise applications.", school: "Example University, Computer Science", years: "2022-2025" },
];
type Run = { id: string; status: string; contentHash: string; errorCode?: string; result?: { facts: { category: string; value: string; quote: string; page: number; start: number }[]; warnings: string[] }; attempts: { usageCostUsd: number }[] };
async function login(page: Page) {
  let token = "";
  page.on("request", r => { if (r.url().startsWith(`${api}/api/`) && r.headers().authorization) token = r.headers().authorization; });
  await page.goto("/"); await page.getByRole("button", { name: "Personel girişi", exact: true }).click();
  await page.locator("#username").fill("hr.specialist");
  await page.locator("#password").fill(process.env.E2E_PASSWORD ?? "NexusHR123!");
  await page.locator("#kc-login").click(); await expect.poll(() => token).toMatch(/^Bearer /);
  return { Authorization: token };
}
for (const [index, cv] of cases.entries()) test(`real-corpus ${cv.id}: PDF -> worker -> Ollama -> evidence -> PostgreSQL -> UI`, async ({ page, browser, request }) => {
  test.skip(process.env.RUN_OLLAMA_CORPUS !== "1", "Opt-in real local-model corpus; no external AI or model inference in normal CI.");
  test.setTimeout(780_000);
  const headers = await login(page);
  const reuse = process.env.REUSE_OLLAMA_SYNTHETIC_CORPUS === "1";
  let candidate: { id: string; email: string; firstName: string; lastName: string };
  if (reuse) {
    // Explicit recovery of this suite's acknowledged synthetic records, never select
    // a successful sibling to conceal a failed run. Recheck the latest run below.
    const existing = await (await request.get(`${api}/api/candidates`, { headers, params: { search: `synthetic-corpus-${cv.id}-`, pageSize: 20 } })).json();
    expect(existing.items).toHaveLength(1);
    candidate = existing.items[0];
    expect(candidate.firstName).toBe("Synthetic"); expect(candidate.lastName).toBe("Expert");
    expect(candidate.email).toMatch(new RegExp(`^synthetic-corpus-${cv.id}-[0-9]+@example\\.test$`));
  } else {
    const email = `synthetic-corpus-${cv.id}-${Date.now()}@example.test`;
    const created = await request.post(`${api}/api/candidates`, { headers, data: { firstName: "Synthetic", lastName: "Expert", email, phoneNumber: "05555555555" } });
    expect(created.status()).toBe(201); candidate = await created.json(); candidate.email = email;
  }
  const email = candidate.email;
  const tr = cv.lang === "tr";
  const source = ["Synthetic Expert", email, "05555555555",
    `${tr ? "Profesyonel özet" : "Professional summary"}: ${cv.role}. ${cv.work}`,
    `${tr ? "Teknik beceriler" : "Technical skills"}: ${cv.skills}`,
    `${tr ? "İş deneyimi" : "Employment"}: ${cv.years} ${cv.company}. ${cv.role}. ${cv.work}`,
    `${tr ? "Eğitim" : "Education"}: 2015-2019 ${cv.school}.`,
    `${tr ? "Sertifika" : "Certification"}: 2023 Synthetic Software Testing Certificate.`,
    `${tr ? "Yabancı diller" : "Languages"}: ${tr ? "İngilizce B2, Türkçe C2" : "English C1, German B1"}.`].join("\n");
  if (!reuse) {
    const pdfPage = await browser.newPage();
    await pdfPage.setContent(`<html lang="${cv.lang}"><meta charset="utf-8"><style>body{font:15px Arial;line-height:1.6;padding:30px}p{white-space:pre-wrap;max-width:${index % 2 ? "520" : "650"}px}</style><h1>${cv.role}</h1><p>${source}</p></html>`);
    const pdf = await pdfPage.pdf({ format: "A4", printBackground: true }); await pdfPage.close();
    expect((await request.post(`${api}/api/candidates/${candidate.id}/documents/cv`, { headers, multipart: { file: { name: `${cv.id}.pdf`, mimeType: "application/pdf", buffer: pdf } } })).status()).toBe(201);
  }
  const base = `${api}/api/candidates/${candidate.id}/cv-analyses`;
  const history = await (await request.get(base, { headers })).json();
  expect(history.settings.provider).toBe("Ollama"); expect(history.settings.externalProcessingAllowed).toBe(false);
  const key = crypto.randomUUID(); const body = { documentId: history.documentId };
  let run: Run;
  if (reuse) { expect(history.items.length).toBeGreaterThan(0); run = history.items[0]; }
  else { const started = await request.post(base, { headers: { ...headers, "Idempotency-Key": key }, data: body }); expect(started.status()).toBe(202); run = await started.json(); }
  let final: Run | undefined;
  const wait = async (id: string) => {
    await expect.poll(async () => {
      final = await (await request.get(`${base}/${id}`, { headers })).json();
      return ["Queued", "Running"].includes(final!.status) ? "pending" : "finished";
    }, { timeout: 330_000, intervals: [2000] }).toBe("finished");
  };
  await wait(run.id);
  const evidenceDirectory = resolve("../.tools/evidence-corpus"); await mkdir(evidenceDirectory, { recursive: true });
  // Synthetic fixtures/results only. Never run this capture against real user documents.
  await writeFile(`${evidenceDirectory}/${cv.id}.json`, JSON.stringify({ case: cv.id, language: cv.lang, email, run: final }, null, 2));
  expect(final!.status, `${cv.id}: ${final!.errorCode ?? "no error"}`).toBe("Succeeded");
  const facts = final!.result!.facts;
  for (const category of ["skill", "employment", "education"])
    expect(facts.some(f => f.category === category), `${cv.id} missing ${category}`).toBe(true);
  expect(facts.every(f => f.start >= 0 && f.page === 1 && f.quote.includes(f.value))).toBe(true);
  const download = await request.get(`${api}/api/candidates/${candidate.id}/documents/cv`, { headers }); expect(download.ok()).toBe(true);
  const storedPdf = await download.body(); expect(createHash("sha256").update(storedPdf).digest("hex")).toBe(final!.contentHash);
  // Validate exact PdfPig spans against the downloaded immutable PDF in the .NET
  // corpus grounding test, not against HTML text with different layout/redaction.
  await writeFile(`${evidenceDirectory}/${cv.id}.pdf`, storedPdf);
  await writeFile(`${evidenceDirectory}/${cv.id}.json`, JSON.stringify({ case: cv.id, email, run: final }, null, 2));
  expect(final!.attempts.every(a => a.usageCostUsd === 0)).toBe(true);
  if (!reuse) { const replay = await request.post(base, { headers: { ...headers, "Idempotency-Key": key }, data: body }); expect((await replay.json()).id).toBe(run.id); }
  await page.goto(`/candidates/${candidate.id}`); await page.getByRole("tab", { name: "CV Analizi", exact: true }).click();
  await expect(page.getByText(/Kaynak: sayfa 1, konum/).first()).toBeVisible();
  await expect(page.getByText(`Beceri: ${facts.find(f => f.category === "skill")!.value}`, { exact: true })).toBeVisible();
  await expect(page.getByText(`İş deneyimi: ${facts.find(f => f.category === "employment")!.value}`, { exact: true })).toBeVisible();
  await expect(page.getByText(`Eğitim: ${facts.find(f => f.category === "education")!.value}`, { exact: true })).toBeVisible();
  const repeats = process.env.OLLAMA_CORPUS_REPEATS ?? "both";
  if ((index === 0 && (repeats === "both" || repeats === "tr")) || (index === 5 && (repeats === "both" || repeats === "en"))) {
    const repeatKey = crypto.randomUUID();
    const repeated = await request.post(base, { headers: { ...headers, "Idempotency-Key": repeatKey }, data: body });
    expect(repeated.status()).toBe(202); const again = await repeated.json(); expect(again.id).not.toBe(run.id);
    await wait(again.id); expect(final!.status).toBe("Succeeded");
    const replay = await request.post(base, { headers: { ...headers, "Idempotency-Key": repeatKey }, data: body }); expect((await replay.json()).id).toBe(again.id);
    await writeFile(`${evidenceDirectory}/${cv.id}-repeat.json`, JSON.stringify({ case: `${cv.id}-repeat`, run: final }, null, 2));
  }
});
