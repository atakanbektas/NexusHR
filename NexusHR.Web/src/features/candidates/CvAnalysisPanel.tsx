import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Card, CardContent, Checkbox, Chip, FormControlLabel, Stack, TextField, Typography } from "@mui/material";
import { candidateApi } from "../../api/candidateApi";
import { hasAnyRole, NexusHrRoles } from "../../auth/roles";
import axios from "axios";
import CvDocumentPanel from "./CvDocumentPanel";
import CvEvidenceTables, { type EvidenceFact } from "./CvEvidenceTables";

type Fact = EvidenceFact;
type Review = { reviewer: string; decision: string; notes: string; createdAtUtc: string; revision: number; factIndex?: number | null; correctedValue?: string | null };
type Run = {
  id: string; documentId: string; status: string; version: number; cancellationRequested: boolean; provider?: string;
  createdAtUtc: string; errorCode: string | null; model: string;
  result: { facts: Fact[]; warnings: string[]; experienceCalendarMonths?: number | null } | null; reviews: Review[]; stage?: string;
  attempts: { number: number; outcome: string; reservedUsd: number; usageCostUsd: number | null; priceVersion: string }[];
};
type History = {
  settings: { enabled: boolean; externalProcessingAllowed: boolean; provider: string; maxReservationUsd: number; ready?: boolean; unavailableCode?: string };
  documentId: string | null; items: Run[]; page: number; totalCount: number;
};
const labels: Record<string, string> = { Queued: "Kuyrukta", Running: "Çalışıyor", Succeeded: "Tamamlandı", Failed: "Başarısız", Refused: "Sağlayıcı reddetti", OutcomeUnknown: "Çağrı sonucu belirsiz", Cancelled: "İptal edildi", NoFindings: "Doğrulanmış sonuç yok" };
function legacyEmpty(run: Run) { return run.status === "Succeeded" && (!run.result || run.result.facts.length === 0); }
function runLabel(run: Run) { return legacyEmpty(run) ? "Eski boş analiz" : labels[run.status] ?? run.status; }
const categories: Record<string, string> = { summary: "Profesyonel özet", skill: "Beceri", employment: "İş deneyimi", education: "Eğitim", certification: "Sertifika", language: "Dil", project: "Projeler", technology: "Kullanılan teknolojiler", experience: "Beyan edilen deneyim süresi", specialization: "Pozisyon ve uzmanlık" };
const explanations: Record<string, string> = {
  "AI.ModelNotReady": "Seçilen yerel model henüz indirilmemiş veya hazır değil. Model kurulumunu kontrol edin.",
  "AI.OllamaUnavailable": "Yerel AI servisine ulaşılamıyor. Docker Ollama servisinin çalıştığını kontrol edin.",
  "AI.OllamaBusy": "Yerel model meşgul. Sınırlı yeniden deneme sonrasında tekrar deneyebilirsiniz.",
  "AI.InvalidResponse": "Model geçerli ve kaynakla doğrulanabilen bir sonuç üretmedi. Sonuç kaydedilmedi; yeniden deneyebilirsiniz.",
  "AI.InvalidInputOrEvidence": "CV metni veya üretilen kaynak kanıtları doğrulanamadı.",
  "AI.Timeout": "Yerel analiz süre sınırını aştı. CPU/RAM kaynaklarını veya yapılandırılan süreyi kontrol edin.",
  "AI.IncompleteResponse": "Model yanıtı tamamlanmadı. Çıktı sınırını veya daha kısa bir CV kullanmayı değerlendirin.",
  "AI.InputTooLarge": "CV metni modelin güvenli bağlam sınırını aşıyor. Daha kısa bir CV veya daha büyük bağlam yapılandırın.",
  "AI.UserQuotaExceeded": "Günlük analiz istek kotanız doldu.",
  "AI.Disabled": "AI analizi bu ortamda kapalı.",
  "AI.LocalModelRequired": "Yalnızca indirilmiş yerel modeller kullanılabilir; bulut modeline CV gönderilmez.",
  "AI.NoReadableText": "PDF’den okunabilir metin çıkarılamadı. Görsel/tarama tabanlıysa yerel OCR profilini etkinleştirin. Metin içeren PDF veya manuel inceleme de kullanılabilir.",
  "AI.NoAnalyzableText": "İletişim bilgileri gizlendikten sonra analiz edilebilir metin kalmadı. Model çağrılmadı. Kayıtlı PDF’nin mesleki bilgi içerdiğini kontrol edin; bilgiler uydurulmaz.",
  "AI.NoSupportedFacts": "Model kaynakla doğrulanabilir profesyonel bilgi üretmedi. Bu, CV’nizde bilgi olmadığı anlamına gelmez. Yeniden deneyebilir veya manuel inceleme yapabilirsiniz.",
  "AI.EmptyResultCannotBeReviewed": "Boş analiz onaylanamaz. Mesleki bilgi içeren CV ile yeni bir analiz başlatın.",
  "Candidate.UnsafeDocument": "Belge güvenlik taramasını geçemedi. Dosya işlenmedi; güvenli bir belge sağlayın.",
  "Candidate.ScanUnavailable": "Belge güvenlik taraması şu anda yapılamıyor. Güvenli biçimde durduruldu; daha sonra tekrar deneyin.",
  "AI.OcrDisabled": "Bu ortamda yerel OCR kapalı. Görsel/tarama belgeleri için multimodal Docker profilini başlatın.",
  "AI.OcrUnavailable": "Yerel OCR servisine erişilemiyor. Docker servis durumunu kontrol edin.",
  "AI.OcrTimeout": "OCR süre sınırını aştı. Daha az sayfalı ve okunaklı bir belge kullanın.",
  "AI.OcrBusy": "Yerel OCR meşgul. Sınırlı yeniden denemeden sonra tekrar deneyebilirsiniz.",
  "AI.OcrInvalidDocument": "Belge çözümlenemedi veya OCR sınırlarını aşıyor: en fazla 8 sayfa ve görsel başına 16 milyon piksel.",
  "AI.OcrNoReadableText": "Yerel OCR okunabilir metin bulamadı. Daha kaliteli tarama veya manuel inceleme gerekiyor.",
  "AI.LocalImagesDisabled": "Görsel modeline belge aktarımı bu ortamda kapalı.",
};
function explain(code: string) { return explanations[code] ?? `İşlem tamamlanamadı (${code}). Güncel kaydı kontrol edin.`; }

function errorText(error: unknown) {
  const code = axios.isAxiosError(error) ? error.response?.data?.code : null;
  return code ? explain(String(code)) : "Analiz servisine erişilemedi. Tekrar deneyin.";
}

export default function CvAnalysisPanel({ candidateId, candidateStatus }: { candidateId: string; candidateStatus?: string }) {
  const [history, setHistory] = useState<History | null>(null);
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [consent, setConsent] = useState(false);
  const [sourcePage, setSourcePage] = useState(1);
  const [corrections, setCorrections] = useState<Record<string, string>>({});
  const [acknowledgeUnknown, setAcknowledgeUnknown] = useState(false);
  const [notes, setNotes] = useState("");
  // Preserve the same key after an uncertain HTTP outcome, until an analysis is acknowledged.
  const [requestKey, setRequestKey] = useState(() => crypto.randomUUID());
  const canEdit = hasAnyRole([NexusHrRoles.HrSpecialist]);
  const base = `/api/candidates/${candidateId}/cv-analyses`;
  const load = useCallback(async () => {
    const response = await candidateApi.get<History>(base, { params: { page } });
    setHistory(response.data);
  }, [base, page]);
  useEffect(() => {
    let active = true;
    const refresh = async () => {
      try {
        const response = await candidateApi.get<History>(base, { params: { page } });
        if (active) setHistory(response.data);
      } catch (e) { if (active) setError(errorText(e)); }
    };
    void refresh();
    const timer = setInterval(() => { void refresh(); }, 3000);
    return () => { active = false; clearInterval(timer); };
  }, [base, page]);

  const selected = history?.items.find(x => x.id === selectedId) ?? history?.items[0];
  async function act(operation: () => Promise<unknown>) {
    setBusy(true); setError("");
    try { await operation(); await load(); } catch (e) { setError(errorText(e)); }
    finally { setBusy(false); }
  }
  const headers = { "If-Match": `"${selected?.version}"` };
  return <Card sx={{ mt: 3 }}><CardContent><Stack spacing={2}>
    <Typography variant="h5">CV Analizi</Typography>
    <Alert severity="info">Analiz yardımcı bir taslaktır. Kaynak kanıtlarını insan incelemelidir. İşe alım kararını veya belge doğrulamasını otomatik değiştirmez.</Alert>
    {error && <Alert severity="error">{error}</Alert>}
    {!history && <Button onClick={() => void act(load)} disabled={busy}>Yeniden yükle</Button>}
    {history && <>
      <Typography sx={{ fontWeight: 700 }}>Sağlayıcı: {history.settings.provider === "Ollama" ? "Yerel AI — Ollama" : history.settings.provider}</Typography>
      {history.settings.provider === "Ollama" && <Alert severity="success">Harici API maliyeti: 0 USD. Analiz yerel Docker kaynaklarını kullanır; CPU/RAM tüketir.</Alert>}
      {history.settings.ready === false && <Alert severity="warning">{explain(history.settings.unavailableCode ?? "AI.OllamaUnavailable")}</Alert>}
      {!history.settings.enabled && <Alert severity="info">AI özelliği bu ortamda kapalıdır. Mevcut işe alım akışı kullanılabilir.</Alert>}
      {!history.documentId && <Alert severity="warning">Analiz için kayıtlı bir CV gereklidir.</Alert>}
      {canEdit && <>
        <Typography variant="body2">Çağrı başına ayrılacak üst bütçe: {history.settings.maxReservationUsd.toFixed(6)} USD. Sonucu belirsiz çağrının rezervasyonu korunur.</Typography>
        {history.settings.externalProcessingAllowed && <FormControlLabel control={<Checkbox checked={consent} onChange={e => setConsent(e.target.checked)} />} label="CV metninin dış AI sağlayıcısına aktarılacağını ve kurumun veri işleme kurallarını kontrol ettiğimi onaylıyorum." />}
        <Button variant="contained" disabled={busy || !history.settings.enabled || history.settings.ready === false || !history.documentId || (history.settings.externalProcessingAllowed && !consent) || history.items.some(x => x.status === "Queued" || x.status === "Running")}
          onClick={() => void act(async () => {
            const response = await candidateApi.post<Run>(base, { documentId: history.documentId, acknowledgeExternalProcessing: consent }, { headers: { "Idempotency-Key": requestKey } });
            setSelectedId(response.data.id); setRequestKey(crypto.randomUUID()); setPage(1);
          })}>Analiz başlat</Button>
      </>}
      <CvDocumentPanel candidateId={candidateId} canEdit={canEdit && candidateStatus === "DocumentsPending"} documentId={selected?.documentId ?? history.documentId ?? undefined} page={sourcePage} />
      <Typography variant="h6">Analiz geçmişi</Typography>
      {history.items.length === 0 && <Typography>Henüz analiz yok.</Typography>}
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }} useFlexGap>
        {history.items.map(run => <Button key={run.id} variant={selected?.id === run.id ? "contained" : "outlined"} onClick={() => { setSelectedId(run.id); setNotes(""); setAcknowledgeUnknown(false); }}>
          {new Date(run.createdAtUtc).toLocaleString("tr-TR")} — {runLabel(run)}
        </Button>)}
      </Stack>
      <Stack direction="row" spacing={1}>
        <Button disabled={page === 1 || busy} onClick={() => { setPage(page - 1); setSelectedId(null); }}>Önceki</Button>
        <Typography sx={{ alignSelf: "center" }}>Sayfa {page}</Typography>
        <Button disabled={page * 20 >= history.totalCount || busy} onClick={() => { setPage(page + 1); setSelectedId(null); }}>Sonraki</Button>
      </Stack>
      {selected && <Box><Stack spacing={2}>
        <Chip label={runLabel(selected)} sx={{ alignSelf: "start" }} />
        <Typography variant="body2">Model: {selected.model}</Typography>
        {selected.status === "Running" && <Typography>Aşama: {({ Extracting: "Metin/OCR çıkarılıyor", Analyzing: "Metin analiz ediliyor", VisualReview: "Görseller inceleniyor" } as Record<string, string>)[selected.stage ?? ""] ?? "İşleniyor"}</Typography>}
        <Typography variant="body2">Analiz sağlayıcısı: {selected.provider === "Ollama" ? "Yerel AI — Ollama" : selected.provider ?? "OpenAI"}</Typography>
        {selected.errorCode && <Alert severity="warning">{explain(selected.errorCode)}</Alert>}
        {selected.cancellationRequested && <Alert severity="info">{selected.provider === "Ollama" ? "İptal istendi. Harici API ücreti yoktur." : "İptal istendi. Gönderilmiş çağrı ücretlenebilir."}</Alert>}
        {selected.status === "OutcomeUnknown" && <Alert severity="warning">Sağlayıcı çağrısı ücretlenmiş olabilir. Otomatik tekrar yapılmadı.</Alert>}
        {legacyEmpty(selected) && <Alert severity="warning">Eski sürüm boş yanıtı başarılı kaydetmiş. Bu kayıt kullanılabilir bir analiz değildir ve onaylanamaz. Aynı CV için Analiz başlat ile yeni kayıt oluşturabilirsiniz.</Alert>}
        {!legacyEmpty(selected) && selected.result?.warnings.map((warning, i) => <Alert key={i} severity="warning">{warning}</Alert>)}
        {selected.result && <CvEvidenceTables facts={selected.result.facts} months={selected.result.experienceCalendarMonths} reviews={selected.reviews} />}
        {selected.result && Object.keys(categories).some(category => !selected.result!.facts.some(f => f.category === category)) && <Alert severity="info">
          Bu analizde kaynakla doğrulanmış bulgu üretilmeyen alanlar: {Object.entries(categories).filter(([category]) => !selected.result!.facts.some(f => f.category === category)).map(([, label]) => label).join(", ")}. Eksik bilgiler tamamlanmaz.
        </Alert>}
        {selected.result && Object.entries(categories).filter(([category]) => selected.result!.facts.some(f => f.category === category)).map(([category, label]) => <Stack key={category} spacing={1}>
          <Typography variant="h6">{label === "Beceri" ? "Teknik beceriler" : label === "Dil" ? "Yabancı diller" : label}</Typography>
          {!selected.result!.facts.some(fact => fact.category === category) && <Typography color="text.secondary">Bu alan için doğrulanmış kaynak bilgisi bulunamadı.</Typography>}
          {selected.result!.facts.filter(fact => fact.category === category).map((fact, i) => <Box key={i} sx={{ borderLeft: 3, borderColor: "primary.main", pl: 2 }}>
          <Typography sx={{ fontWeight: 700 }}>{categories[fact.category] ?? fact.category}: {fact.value}</Typography>
          <Typography>Kaynak: sayfa {fact.page}, konum {fact.start}</Typography>
          <Typography component="blockquote" sx={{ whiteSpace: "pre-wrap", m: 0 }}>“{fact.quote}”</Typography>
          <Typography variant="caption">Çıkarım kaynağı: {fact.source ?? "pdfpig"}; otomatik doğrulama yalnızca kaynak eşleşmesidir, insan onayı değildir.</Typography>
          {!!fact.regions?.length && <Typography variant="caption" sx={{ display: "block" }}>Görsel bölgeler: {fact.regions.map(r => `(${r.x}, ${r.y}, ${r.width}×${r.height})`).join("; ")}</Typography>}
          <Button onClick={() => { setSourcePage(fact.page); }}>Kaynak sayfaya git: {fact.page}</Button>
          {canEdit && selected.status === "Succeeded" && (() => {
            const index = selected.result!.facts.indexOf(fact);
            const correctionKey = `${selected.id}:${index}`;
            const review = selected.reviews.find(r => r.factIndex === index);
            return <Stack spacing={1}>
              {review && <Alert severity="info">Bulgu incelemesi: {review.decision}{review.correctedValue ? ` — İnsan düzeltmesi: ${review.correctedValue}` : ""}. Orijinal model değeri korunur.</Alert>}
              <TextField label={`Bulgu ${index + 1} için insan düzeltmesi`} value={corrections[correctionKey] ?? ""} onChange={e => setCorrections({ ...corrections, [correctionKey]: e.target.value })} slotProps={{ htmlInput: { maxLength: 500 } }} />
              <Stack direction="row" spacing={1}>{[["approve", "Bulguyu kabul et"], ["reject", "Bulguyu reddet"], ["correct", "Bulguyu düzelt"]].map(([decision, label]) =>
                <Button key={decision} aria-label={`${label}: bulgu ${index + 1}`} disabled={busy || (decision === "correct" && !corrections[correctionKey]?.trim())} onClick={() => void act(() => candidateApi.put(`${base}/${selected.id}/review`, {
                  decision, factIndex: index, correctedValue: decision === "correct" ? corrections[correctionKey] : null,
                }, { headers }))}>{label}</Button>)}</Stack>
            </Stack>;
          })()}
          </Box>)}
        </Stack>)}
        {selected.attempts.map(attempt => <Typography key={attempt.number} variant="body2">Deneme {attempt.number}: {legacyEmpty(selected) && attempt.outcome === "Succeeded" ? "Eski boş sonuç" : labels[attempt.outcome] ?? attempt.outcome}; maliyet {attempt.usageCostUsd === null ? "belirsiz — rezervasyon korunuyor" : `${attempt.usageCostUsd.toFixed(6)} USD`}; fiyat sürümü {attempt.priceVersion}</Typography>)}
        {canEdit && (selected.status === "Queued" || selected.status === "Running") && <Button disabled={busy || selected.cancellationRequested} onClick={() => void act(() => candidateApi.post(`${base}/${selected.id}/cancel`, {}, { headers }))}>Analizi iptal et</Button>}
        {canEdit && (selected.status === "Failed" || selected.status === "NoFindings" || selected.status === "OutcomeUnknown") && <>
          {selected.status === "OutcomeUnknown" && <FormControlLabel control={<Checkbox checked={acknowledgeUnknown} onChange={e => setAcknowledgeUnknown(e.target.checked)} />} label="Tekrar denemenin ek ücret oluşturabileceğini kabul ediyorum." />}
          <Button disabled={busy || !history.settings.enabled || history.settings.ready === false || (selected.status === "OutcomeUnknown" && !acknowledgeUnknown)} onClick={() => void act(() => candidateApi.post(`${base}/${selected.id}/retry`, { acknowledgePossibleDuplicateCharge: acknowledgeUnknown }, { headers }))}>Yeniden analiz et</Button>
        </>}
        {canEdit && selected.status === "Succeeded" && !legacyEmpty(selected) && <>
          <TextField label="İnsan incelemesi / düzeltmeler" multiline minRows={3} value={notes} onChange={e => setNotes(e.target.value)} slotProps={{ htmlInput: { maxLength: 4000 } }} />
          <Stack direction="row" spacing={1}>
            {[["approve", "Onayla"], ["reject", "Reddet"], ["correct", "Düzeltme kaydet"]].map(([decision, label]) => <Button key={decision} disabled={busy || (decision === "correct" && !notes.trim())} onClick={() => void act(async () => { await candidateApi.put(`${base}/${selected.id}/review`, { decision, notes }, { headers }); setNotes(""); })}>{label}</Button>)}
          </Stack>
        </>}
        {selected.reviews.map(review => <Alert severity="info" key={review.revision}>İnceleme #{review.revision}: {review.decision} — {review.notes || "Not eklenmedi"} ({new Date(review.createdAtUtc).toLocaleString("tr-TR")})</Alert>)}
      </Stack></Box>}
    </>}
  </Stack></CardContent></Card>;
}
