import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Stack, Typography } from "@mui/material";
import { candidateApi } from "../../api/candidateApi";
import axios from "axios";

type Version = { id: string; revision: number; isCurrent: boolean; fileName: string; contentType: string; uploadedAtUtc: string };
export default function CvDocumentPanel({ candidateId, canEdit, documentId, page = 1 }: {
  candidateId: string; canEdit: boolean; documentId?: string; page?: number;
}) {
  const [versions, setVersions] = useState<Version[]>([]);
  const [preview, setPreview] = useState<{ id: string; url: string; mime: string } | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [manualPreview, setManualPreview] = useState<string | null>(null);
  const effectiveId = manualPreview ?? documentId;
  const base = `/api/candidates/${candidateId}/documents/cv`;
  const load = useCallback(async () => { setVersions((await candidateApi.get<Version[]>(`${base}/versions`)).data); }, [base]);
  useEffect(() => {
    let active = true;
    void candidateApi.get<Version[]>(`${base}/versions`).then(response => { if (active) setVersions(response.data); })
      .catch(() => { if (active) setError("CV sürümleri yüklenemedi."); });
    return () => { active = false; };
  }, [base]);
  useEffect(() => {
    if (!effectiveId) return;
    let active = true; let url: string | undefined;
    void candidateApi.get<Blob>(`${base}/versions/${effectiveId}`, { responseType: "blob" }).then(response => {
      if (!active) return;
      const mime = response.data.type;
      if (!["application/pdf", "image/jpeg", "image/png", "image/webp"].includes(mime)) throw new Error("mime");
      url = URL.createObjectURL(response.data); setPreview({ id: effectiveId, url, mime });
    }).catch(() => { if (active) setError("Kaynak belge önizlemesi açılamadı."); });
    return () => { active = false; if (url) URL.revokeObjectURL(url); };
  }, [base, effectiveId]);
  const current = versions.find(v => v.isCurrent);
  const shown = versions.find(v => v.id === effectiveId);
  return <Stack spacing={1}>
    <Typography variant="h6">CV sürümleri ve kaynak belge</Typography>
    {error && <Alert severity="warning">{error}</Alert>}
    <Typography variant="caption">Önceki dosyalar ve analizleri korunur. CV yalnızca belge doğrulaması öncesinde güncellenebilir.</Typography>
    {versions.map(v => <Stack direction="row" key={v.id} spacing={1} sx={{ alignItems: "center" }}>
      <Typography>Sürüm {v.revision}{v.isCurrent ? " (güncel)" : ""} — {v.fileName}</Typography>
      <Button onClick={() => setManualPreview(v.id)}>Sürüm {v.revision} önizle</Button>
    </Stack>)}
    {canEdit && current && <Button component="label" disabled={busy}>CV’yi yeni sürümle güncelle
      <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png,.webp" onChange={e => {
        const file = e.target.files?.[0]; e.target.value = ""; if (!file) return;
        if (file.size > 5 * 1024 * 1024) { setError("CV en fazla 5 MB olabilir."); return; }
        setBusy(true); setError(""); const form = new FormData(); form.append("file", file);
        void candidateApi.put(base, form, { headers: { "If-Match": `"${current.id}"` } }).then(load).catch(error => {
          const code = axios.isAxiosError(error) ? error.response?.data?.code : "";
          setError(code === "Candidate.CvVersionConflict" ? "CV başka bir işlemde güncellendi. Sürümleri yeniden yükleyin." :
            "CV güncellenemedi. Dosya türünü, OCR servisini ve adayın doğrulama durumunu kontrol edin.");
        }).finally(() => setBusy(false));
      }} />
    </Button>}
    {manualPreview && <Button onClick={() => setManualPreview(null)}>Sürüm önizlemesini kapat</Button>}
    {preview && preview.id === effectiveId && <Box sx={{ border: 1, borderColor: "divider" }}>
      {shown && <Typography>Önizlenen belge: Sürüm {shown.revision}{shown.isCurrent ? " (güncel)" : " (önceki sürüm)"}</Typography>}
      {shown && !shown.isCurrent && <Alert severity="info">Gösterilen kaynak önceki CV sürümüdür. Güncel dosyayı değerlendirmek için yeni analiz başlatın.</Alert>}
      <Typography>Kaynak sayfa: {page}</Typography>
      {preview.mime === "application/pdf" ? <object aria-label={`CV kaynak sayfa ${page}`} data={`${preview.url}#page=${page}`} type="application/pdf" width="100%" height="600">
        <a href={preview.url} target="_blank" rel="noreferrer">Kaynak PDF’yi aç</a>
      </object> : <Box component="img" src={preview.url} alt="CV kaynak görseli" sx={{ width: "100%", maxHeight: 700, objectFit: "contain" }} />}
    </Box>}
  </Stack>;
}
