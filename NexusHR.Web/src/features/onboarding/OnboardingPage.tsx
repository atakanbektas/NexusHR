import { useEffect, useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import axios from "axios";
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress,
  Container, LinearProgress, Stack, Typography } from "@mui/material";
import { hasAnyRole, NexusHrRoles } from "../../auth/roles";
import { completeOnboarding, getOnboarding, getOnboardings, updateTask } from "./onboardingService";
import type { Onboarding } from "./onboardingService";

function errorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 409) return "Kayıt değişti veya işlem bu durumda yapılamaz. Yenileyip tekrar deneyin.";
    return error.response?.data?.detail ?? "İşlem gerçekleştirilemedi. Tekrar deneyin.";
  }
  return "İşlem gerçekleştirilemedi. Tekrar deneyin.";
}

export default function OnboardingPage() {
  const { id } = useParams();
  const [items, setItems] = useState<Onboarding[] | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const canEdit = hasAnyRole([NexusHrRoles.HrSpecialist, NexusHrRoles.ItSpecialist]);
  const canComplete = hasAnyRole([NexusHrRoles.HrSpecialist]);
  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      try {
        const result = id ? [await getOnboarding(id, controller.signal)] : await getOnboardings(controller.signal);
        if (!controller.signal.aborted) { setItems(result); setError(""); }
      } catch (failure) {
        if (!controller.signal.aborted) setError(errorMessage(failure));
      }
    }
    void load();
    return () => controller.abort();
  }, [id, revision]);
  async function run(action: () => Promise<void>) {
    setBusy(true); setError("");
    try { await action(); setRevision(value => value + 1); }
    catch (failure) { setError(errorMessage(failure)); }
    finally { setBusy(false); }
  }
  return <Container maxWidth="lg" sx={{ py: 4 }}>
    <Stack direction="row" sx={{ flexWrap: "wrap", gap: 2, justifyContent: "space-between", mb: 3 }}>
      <Typography variant="h4">Çalışan onboarding</Typography>
      <Button onClick={() => setRevision(value => value + 1)} disabled={busy}>Yenile</Button>
      {id && <Button component={RouterLink} to="/onboardings">Tüm kayıtlar</Button>}
    </Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {items === null && !error && <CircularProgress aria-label="Yükleniyor" />}
    {items?.length === 0 && <Alert severity="info">Henüz onboarding kaydı yok. Kabul edilen teklifler işlendiğinde burada görünür.</Alert>}
    <Stack spacing={3}>{items?.map(item => {
      const tasks = item.tasks ?? [];
      const done = tasks.filter(task => task.status === "Completed").length;
      return <Card key={item.id} variant="outlined"><CardContent>
        <Stack direction="row" sx={{ flexWrap: "wrap", gap: 2, alignItems: "center" }}>
          <Typography variant="h6">{item.fullName ?? `${item.firstName} ${item.lastName}`}</Typography>
          <Chip label={item.status} color={item.status === "Completed" ? "success" : "default"} />
        </Stack>
        <Typography color="text.secondary">{item.positionTitle} · {item.department} · {item.proposedStartDate}</Typography>
        {hasAnyRole([NexusHrRoles.HrSpecialist, NexusHrRoles.HrManager, NexusHrRoles.DepartmentManager, NexusHrRoles.Auditor]) &&
          <Button component={RouterLink} to={`/hiring-processes/${item.hiringProcessId}`}>İşe alım süreci</Button>}
        {!id && <Button component={RouterLink} to={`/onboardings/${item.id}`}>Görevleri görüntüle</Button>}
        {id && <>
          <Typography sx={{ mt: 2 }}>{done} / {tasks.length} görev tamamlandı</Typography>
          <LinearProgress variant="determinate" value={tasks.length ? done / tasks.length * 100 : 0} sx={{ my: 2 }} />
          <Stack spacing={2}>{tasks.map(task => <Box key={task.id} sx={{ border: 1, borderColor: "divider", p: 2, borderRadius: 1 }}>
            <Typography sx={{ fontWeight: 600 }}>{task.title}</Typography>
            <Typography variant="body2">{task.assignedDepartment} · {task.dueDate} · {task.status}</Typography>
            {canEdit && item.status !== "Completed" && item.status !== "Cancelled" && <>
              {task.status === "Pending" && <Button disabled={busy} onClick={() => void run(() => updateTask(item.id, task.id, "InProgress"))}>Başlat</Button>}
              {task.status === "InProgress" && <Button disabled={busy} onClick={() => void run(() => updateTask(item.id, task.id, "Completed"))}>Görevi tamamla</Button>}
            </>}
          </Box>)}</Stack>
          {canComplete && item.status === "ReadyForStart" && <Button variant="contained" sx={{ mt: 3 }} disabled={busy}
            onClick={() => void run(() => completeOnboarding(item.id))}>Onboarding tamamla</Button>}
        </>}
      </CardContent></Card>;
    })}</Stack>
  </Container>;
}
