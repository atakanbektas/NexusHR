import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Card, CardContent, Checkbox, Chip, FormControlLabel, Stack, TextField, Typography } from "@mui/material";
import { candidateApi } from "../../api/candidateApi";
import { hasAnyRole, NexusHrRoles } from "../../auth/roles";
import axios from "axios";

type Fact = { category: string; value: string; page: number; start: number; quote: string };
type Review = { reviewer: string; decision: string; notes: string; createdAtUtc: string; revision: number };
type Run = {
  id: string; documentId: string; status: string; version: number; cancellationRequested: boolean;
  createdAtUtc: string; errorCode: string | null; model: string;
  result: { facts: Fact[]; warnings: string[] } | null; reviews: Review[];
  attempts: { number: number; outcome: string; reservedUsd: number; usageCostUsd: number | null; priceVersion: string }[];
};
type History = {
  settings: { enabled: boolean; externalProcessingAllowed: boolean; provider: string; maxReservationUsd: number };
  documentId: string | null; items: Run[]; page: number; totalCount: number;
};
const labels: Record<string, string> = { Queued: "Kuyrukta", Running: "Çalışıyor", Succeeded: "Tamamlandı", Failed: "Başarısız", Refused: "Sağlayıcı reddetti", OutcomeUnknown: "Çağrı sonucu belirsiz", Cancelled: "İptal edildi" };
const categories: Record<string, string> = { skill: "Beceri", employment: "İş deneyimi", education: "Eğitim", language: "Dil", certification: "Sertifika" };

function errorText(error: unknown) {
  const code = axios.isAxiosError(error) ? error.response?.data?.code : null;
  return code ? `İşlem tamamlanamadı (${String(code)}). Güncel kaydı kontrol edin.` : "Analiz servisine erişilemedi. Tekrar deneyin.";
}

export default function CvAnalysisPanel({ candidateId }: { candidateId: string }) {
  const [history, setHistory] = useState<History | null>(null);
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [consent, setConsent] = useState(false);
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
      {!history.settings.enabled && <Alert severity="info">AI özelliği bu ortamda kapalıdır. Mevcut işe alım akışı kullanılabilir.</Alert>}
      {!history.documentId && <Alert severity="warning">Analiz için kayıtlı bir CV gereklidir.</Alert>}
      {canEdit && <>
        <Typography variant="body2">Çağrı başına ayrılacak üst bütçe: {history.settings.maxReservationUsd.toFixed(6)} USD. Sonucu belirsiz çağrının rezervasyonu korunur.</Typography>
        {history.settings.externalProcessingAllowed && <FormControlLabel control={<Checkbox checked={consent} onChange={e => setConsent(e.target.checked)} />} label="CV metninin dış AI sağlayıcısına aktarılacağını ve kurumun veri işleme kurallarını kontrol ettiğimi onaylıyorum." />}
        <Button variant="contained" disabled={busy || !history.settings.enabled || !history.documentId || (history.settings.externalProcessingAllowed && !consent) || history.items.some(x => x.status === "Queued" || x.status === "Running")}
          onClick={() => void act(async () => {
            const response = await candidateApi.post<Run>(base, { documentId: history.documentId, acknowledgeExternalProcessing: consent }, { headers: { "Idempotency-Key": requestKey } });
            setSelectedId(response.data.id); setRequestKey(crypto.randomUUID()); setPage(1);
          })}>Analiz başlat</Button>
      </>}
      <Typography variant="h6">Analiz geçmişi</Typography>
      {history.items.length === 0 && <Typography>Henüz analiz yok.</Typography>}
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }} useFlexGap>
        {history.items.map(run => <Button key={run.id} variant={selected?.id === run.id ? "contained" : "outlined"} onClick={() => { setSelectedId(run.id); setNotes(""); setAcknowledgeUnknown(false); }}>
          {new Date(run.createdAtUtc).toLocaleString("tr-TR")} — {labels[run.status] ?? run.status}
        </Button>)}
      </Stack>
      <Stack direction="row" spacing={1}>
        <Button disabled={page === 1 || busy} onClick={() => { setPage(page - 1); setSelectedId(null); }}>Önceki</Button>
        <Typography sx={{ alignSelf: "center" }}>Sayfa {page}</Typography>
        <Button disabled={page * 20 >= history.totalCount || busy} onClick={() => { setPage(page + 1); setSelectedId(null); }}>Sonraki</Button>
      </Stack>
      {selected && <Box><Stack spacing={2}>
        <Chip label={labels[selected.status] ?? selected.status} sx={{ alignSelf: "start" }} />
        <Typography variant="body2">Model: {selected.model}</Typography>
        {selected.errorCode && <Alert severity="warning">{selected.errorCode}</Alert>}
        {selected.cancellationRequested && <Alert severity="info">İptal istendi. Gönderilmiş çağrı ücretlenebilir.</Alert>}
        {selected.status === "OutcomeUnknown" && <Alert severity="warning">Sağlayıcı çağrısı ücretlenmiş olabilir. Otomatik tekrar yapılmadı.</Alert>}
        {selected.result?.warnings.map((warning, i) => <Alert key={i} severity="warning">{warning}</Alert>)}
        {selected.result?.facts.length === 0 && <Typography>Desteklenen profesyonel bilgi bulunamadı.</Typography>}
        {selected.result?.facts.map((fact, i) => <Box key={i} sx={{ borderLeft: 3, borderColor: "primary.main", pl: 2 }}>
          <Typography sx={{ fontWeight: 700 }}>{categories[fact.category] ?? fact.category}: {fact.value}</Typography>
          <Typography>Kaynak: sayfa {fact.page}, konum {fact.start}</Typography>
          <Typography component="blockquote" sx={{ whiteSpace: "pre-wrap", m: 0 }}>“{fact.quote}”</Typography>
        </Box>)}
        {selected.attempts.map(attempt => <Typography key={attempt.number} variant="body2">Deneme {attempt.number}: {attempt.outcome}; maliyet {attempt.usageCostUsd === null ? "belirsiz — rezervasyon korunuyor" : `${attempt.usageCostUsd.toFixed(6)} USD`}; fiyat sürümü {attempt.priceVersion}</Typography>)}
        {canEdit && (selected.status === "Queued" || selected.status === "Running") && <Button disabled={busy || selected.cancellationRequested} onClick={() => void act(() => candidateApi.post(`${base}/${selected.id}/cancel`, {}, { headers }))}>Analizi iptal et</Button>}
        {canEdit && (selected.status === "Failed" || selected.status === "OutcomeUnknown") && <>
          {selected.status === "OutcomeUnknown" && <FormControlLabel control={<Checkbox checked={acknowledgeUnknown} onChange={e => setAcknowledgeUnknown(e.target.checked)} />} label="Tekrar denemenin ek ücret oluşturabileceğini kabul ediyorum." />}
          <Button disabled={busy || !history.settings.enabled || (selected.status === "OutcomeUnknown" && !acknowledgeUnknown)} onClick={() => void act(() => candidateApi.post(`${base}/${selected.id}/retry`, { acknowledgePossibleDuplicateCharge: acknowledgeUnknown }, { headers }))}>Yeniden analiz et</Button>
        </>}
        {canEdit && selected.status === "Succeeded" && <>
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
