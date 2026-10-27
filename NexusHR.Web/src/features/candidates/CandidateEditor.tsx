import { useState } from "react";
import { Alert, Button, Card, CardContent, Stack, TextField, Typography } from "@mui/material";
import axios from "axios";
import { getCandidateById, updateCandidate, uploadCandidateCv } from "./candidateService";
import type { CandidateDetail } from "./candidateTypes";

export default function CandidateEditor({ candidate, onSaved }: {
  candidate: CandidateDetail; onSaved: (candidate: CandidateDetail) => void;
}) {
  const [form, setForm] = useState({ firstName: candidate.firstName, lastName: candidate.lastName,
    email: candidate.email, phoneNumber: candidate.phoneNumber });
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function save() {
    setBusy(true); setError("");
    try {
      await updateCandidate(candidate.id, form);
      if (file) await uploadCandidateCv(candidate.id, file);
      onSaved(await getCandidateById(candidate.id));
    } catch (failure) {
      setError(axios.isAxiosError(failure)
        ? failure.response?.data?.detail ?? failure.response?.data?.errors?.join?.(" ") ?? "Kayıt güncellenemedi. Yenileyip tekrar deneyin."
        : "Kayıt güncellenemedi.");
    } finally { setBusy(false); }
  }
  return <Card variant="outlined" sx={{ my: 3 }}><CardContent>
    <Typography variant="h6" sx={{ mb: 2 }}>Aday bilgilerini düzenle</Typography>
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      {Object.entries({ firstName: "Ad", lastName: "Soyad", email: "E-posta", phoneNumber: "Telefon" }).map(([key, label]) =>
        <TextField key={key} label={label} value={form[key as keyof typeof form]} disabled={busy}
          onChange={event => setForm(previous => ({ ...previous, [key]: event.target.value }))} />)}
      {candidate.status === "Draft" && <Button component="label" disabled={busy}>Eksik CV dosyasını seç
        <input hidden type="file" accept="application/pdf,.pdf" onChange={event => setFile(event.target.files?.[0] ?? null)} />
      </Button>}
      {file && <Typography>{file.name}</Typography>}
      <Button disabled={busy} variant="outlined" onClick={() => void save()}>Değişiklikleri kaydet</Button>
    </Stack>
  </CardContent></Card>;
}
