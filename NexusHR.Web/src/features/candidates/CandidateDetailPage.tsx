import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Container,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { ArrowBack, Download } from "@mui/icons-material";
import { Link as RouterLink, useParams } from "react-router-dom";
import {
  downloadCandidateCv,
  getCandidateById,
} from "./candidateService";
import {
  candidateStatusColors,
  candidateStatusLabels,
} from "./candidateStatus";
import type { CandidateDetail } from "./candidateTypes";

export default function CandidateDetailPage() {
  const { id } = useParams();
  const [candidate, setCandidate] = useState<CandidateDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isDownloading, setIsDownloading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      return;
    }

    let isActive = true;

    getCandidateById(id)
      .then((response) => {
        if (isActive) {
          setCandidate(response);
        }
      })
      .catch(() => {
        if (isActive) {
          setErrorMessage("Aday bilgileri yüklenemedi.");
        }
      })
      .finally(() => {
        if (isActive) {
          setIsLoading(false);
        }
      });

    return () => {
      isActive = false;
    };
  }, [id]);

  async function handleDownloadCv() {
    if (!candidate) return;

    setIsDownloading(true);
    setErrorMessage(null);

    try {
      const fileName = `${candidate.firstName}-${candidate.lastName}-cv.pdf`;
      await downloadCandidateCv(candidate.id, fileName);
    } catch {
      setErrorMessage("Adaya ait CV indirilemedi.");
    } finally {
      setIsDownloading(false);
    }
  }

  if (!id) {
    return (
      <Container maxWidth="md" sx={{ py: 6 }}>
        <Alert severity="error">Aday kimliği bulunamadı.</Alert>
      </Container>
    );
  }

  if (isLoading) {
    return (
      <Box sx={{ display: "grid", placeItems: "center", py: 12 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Container maxWidth="md" sx={{ py: { xs: 3, md: 6 } }}>
      <Button
        component={RouterLink}
        to="/candidates"
        startIcon={<ArrowBack />}
        sx={{ mb: 2 }}
      >
        Aday listesine dön
      </Button>

      {errorMessage && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {errorMessage}
        </Alert>
      )}

      {candidate && (
        <Card elevation={0} sx={{ border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: { xs: 3, md: 5 } }}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={2}
              sx={{ justifyContent: "space-between", mb: 3 }}
            >
              <Box>
                <Typography variant="h4" sx={{ fontWeight: 800 }}>
                  {candidate.firstName} {candidate.lastName}
                </Typography>
                <Typography color="text.secondary">{candidate.email}</Typography>
              </Box>

              <Chip
                label={candidateStatusLabels[candidate.status]}
                color={candidateStatusColors[candidate.status]}
                sx={{ alignSelf: { sm: "flex-start" } }}
              />
            </Stack>

            <Divider sx={{ mb: 3 }} />

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                gap: 3,
              }}
            >
              <Box>
                <Typography variant="body2" color="text.secondary">
                  Telefon
                </Typography>
                <Typography>{candidate.phoneNumber}</Typography>
              </Box>
              <Box>
                <Typography variant="body2" color="text.secondary">
                  Kayıt tarihi
                </Typography>
                <Typography>
                  {new Date(candidate.createdAtUtc).toLocaleString("tr-TR")}
                </Typography>
              </Box>
            </Box>

            <Divider sx={{ my: 3 }} />

            <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
              Aday belgeleri
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              Aday kaydı sırasında yüklenen CV dosyasını indirebilirsiniz.
            </Typography>
            <Button
              variant="outlined"
              startIcon={
                isDownloading ? <CircularProgress size={18} /> : <Download />
              }
              disabled={isDownloading}
              onClick={handleDownloadCv}
            >
              {isDownloading ? "İndiriliyor" : "CV’yi indir"}
            </Button>
          </CardContent>
        </Card>
      )}
    </Container>
  );
}
