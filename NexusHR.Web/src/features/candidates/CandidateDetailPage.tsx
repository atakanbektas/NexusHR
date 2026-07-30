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
import {
  ArrowBack,
  CheckCircle,
  Download,
} from "@mui/icons-material";
import {
  Link as RouterLink,
  useParams,
} from "react-router-dom";
import {
  downloadCandidateCv,
  getCandidateById,
  verifyCandidateDocuments,
} from "./candidateService";
import {
  candidateStatusColors,
  candidateStatusLabels,
} from "./candidateStatus";
import type {
  CandidateDetail,
} from "./candidateTypes";
import {
  CandidateCvReaderRoles,
  hasAnyRole,
  NexusHrRoles,
} from "../../auth/roles";
import CreateHiringProcessDialog from
  "../hiring/CreateHiringProcessDialog";
  
import {
  getActiveHiringProcessByCandidateId,
} from "../hiring/hiringService";

import {
  hiringStatusColors,
  hiringStatusLabels,
} from "../hiring/hiringStatus";

import type {
  ActiveHiringProcessLookup,
} from "../hiring/hiringTypes";




export default function CandidateDetailPage() {

      const canDownloadCv =
  hasAnyRole(CandidateCvReaderRoles);

const canVerifyDocuments =
  hasAnyRole([
    NexusHrRoles.HrSpecialist,
  ]);

  const { id } = useParams();

  const [candidate, setCandidate] =
    useState<CandidateDetail | null>(null);

  const [isLoading, setIsLoading] =
    useState(true);

  const [isDownloading, setIsDownloading] =
    useState(false);

  const [isVerifying, setIsVerifying] =
    useState(false);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  const [successMessage, setSuccessMessage] =
    useState<string | null>(null);

    const [
  activeHiringProcess,
  setActiveHiringProcess,
] = useState<
  ActiveHiringProcessLookup | null
>(null);

  useEffect(() => {
    if (!id) {
      setIsLoading(false);
      return;
    }



    let isActive = true;

    async function loadCandidate() {
      try {
const [
  candidateResponse,
  hiringProcessResponse,
] = await Promise.all([
  getCandidateById(id!),
  getActiveHiringProcessByCandidateId(id!),
]);

if (isActive) {
  setCandidate(candidateResponse);
  setActiveHiringProcess(
    hiringProcessResponse,
  );
}
      } catch {
        if (isActive) {
          setErrorMessage(
            "Aday bilgileri yüklenemedi.",
          );
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    }

    void loadCandidate();

    return () => {
      isActive = false;
    };
  }, [id]);

  async function handleDownloadCv() {
    if (!candidate) {
      return;
    }

    setIsDownloading(true);
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      const fileName =
        `${candidate.firstName}-${candidate.lastName}-cv.pdf`;

      await downloadCandidateCv(
        candidate.id,
        fileName,
      );
    } catch {
      setErrorMessage(
        "Adaya ait CV indirilemedi.",
      );
    } finally {
      setIsDownloading(false);
    }
  }

  async function handleVerifyDocuments() {
    if (!candidate) {
      return;
    }

    const isConfirmed = window.confirm(
      "Adayın CV ve iletişim bilgilerini kontrol ettiğinizi onaylıyor musunuz?",
    );

    if (!isConfirmed) {
      return;
    }

    setIsVerifying(true);
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      await verifyCandidateDocuments(
        candidate.id,
      );

      const updatedCandidate =
        await getCandidateById(candidate.id);

      setCandidate(updatedCandidate);

      setSuccessMessage(
        "Adayın belgeleri doğrulandı. Aday artık işe alım sürecine hazır.",
      );
    } catch {
      setErrorMessage(
        "Aday belgeleri doğrulanamadı.",
      );
    } finally {
      setIsVerifying(false);
    }
  }

  if (!id) {
    return (
      <Container
        maxWidth="md"
        sx={{ py: 6 }}
      >
        <Alert severity="error">
          Aday kimliği bulunamadı.
        </Alert>
      </Container>
    );
  }

  if (isLoading) {
    return (
      <Box
        sx={{
          display: "grid",
          placeItems: "center",
          py: 12,
        }}
      >
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box
      sx={{
        minHeight: "100vh",
        background:
          "linear-gradient(135deg, #f4f7fb 0%, #eef2ff 100%)",
      }}
    >
      <Container
        maxWidth="md"
        sx={{ py: { xs: 3, md: 6 } }}
      >
        <Button
          component={RouterLink}
          to="/candidates"
          startIcon={<ArrowBack />}
          sx={{ mb: 2 }}
        >
          Aday listesine dön
        </Button>

        {errorMessage && (
          <Alert
            severity="error"
            sx={{ mb: 2 }}
          >
            {errorMessage}
          </Alert>
        )}

        {successMessage && (
          <Alert
            severity="success"
            sx={{ mb: 2 }}
          >
            {successMessage}
          </Alert>
        )}

        {candidate && (
          <Card
            elevation={0}
            sx={{
              border: "1px solid",
              borderColor: "divider",
              borderRadius: 4,
            }}
          >
            <CardContent
              sx={{ p: { xs: 3, md: 5 } }}
            >
              <Stack
                direction={{
                  xs: "column",
                  sm: "row",
                }}
                spacing={2}
                sx={{
                  justifyContent: "space-between",
                  mb: 3,
                }}
              >
                <Box>
                  <Typography
                    variant="h4"
                    sx={{ fontWeight: 800 }}
                  >
                    {candidate.firstName}{" "}
                    {candidate.lastName}
                  </Typography>

                  <Typography color="text.secondary">
                    {candidate.email}
                  </Typography>
                </Box>

<Chip
  label={
    activeHiringProcess?.status
      ? hiringStatusLabels[
          activeHiringProcess.status
        ]
      : candidateStatusLabels[
          candidate.status
        ]
  }
  color={
    activeHiringProcess?.status
      ? hiringStatusColors[
          activeHiringProcess.status
        ]
      : candidateStatusColors[
          candidate.status
        ]
  }
  sx={{
    alignSelf: {
      sm: "flex-start",
    },
  }}
/>
              </Stack>

              <Divider sx={{ mb: 3 }} />

              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: {
                    xs: "1fr",
                    sm: "1fr 1fr",
                  },
                  gap: 3,
                }}
              >
                <Box>
                  <Typography
                    variant="body2"
                    color="text.secondary"
                  >
                    Telefon
                  </Typography>

                  <Typography>
                    {candidate.phoneNumber}
                  </Typography>
                </Box>

                <Box>
                  <Typography
                    variant="body2"
                    color="text.secondary"
                  >
                    Kayıt tarihi
                  </Typography>

                  <Typography>
                    {new Date(
                      candidate.createdAtUtc,
                    ).toLocaleString("tr-TR")}
                  </Typography>
                </Box>
              </Box>

              <Divider sx={{ my: 3 }} />

              <Typography
                variant="h6"
                sx={{
                  fontWeight: 700,
                  mb: 1,
                }}
              >
                Aday belgeleri
              </Typography>

              <Typography
                color="text.secondary"
                sx={{ mb: 2 }}
              >
                Adayın CV dosyasını indirebilir ve
                belge kontrolünü tamamlayabilirsiniz.
              </Typography>

              <Stack
                direction={{
                  xs: "column",
                  sm: "row",
                }}
                spacing={2}
              >
{canDownloadCv && (
  <Button
    variant="outlined"
    startIcon={
      isDownloading
        ? <CircularProgress size={18} />
        : <Download />
    }
    disabled={
      isDownloading ||
      isVerifying
    }
    onClick={handleDownloadCv}
  >
    {isDownloading
      ? "İndiriliyor"
      : "CV’yi indir"}
  </Button>
)}

                {canVerifyDocuments &&
  candidate.status ===
    "DocumentsPending" && (
                  <Button
                    variant="contained"
                    color="success"
                    startIcon={
                      isVerifying
                        ? (
                          <CircularProgress
                            size={18}
                            color="inherit"
                          />
                        )
                        : <CheckCircle />
                    }
                    disabled={
                      isVerifying ||
                      isDownloading
                    }
                    onClick={
                      handleVerifyDocuments
                    }
                  >
                    {isVerifying
                      ? "Doğrulanıyor"
                      : "Belgeleri doğrula"}
                  </Button>
                )}
              </Stack>

{candidate.status ===
  "ReadyForHiring" && (
  <Stack
    spacing={2}
    sx={{ mt: 3 }}
  >
<Alert
  severity={
    activeHiringProcess?.status ===
    "OfferAccepted"
      ? "success"
      : "info"
  }
>
  {activeHiringProcess?.status ===
  "OfferAccepted"
    ? "Aday iş teklifini kabul etti. İşe giriş işlemleri başlatılabilir."
    : activeHiringProcess?.status
      ? `Adayın işe alım süreci devam ediyor: ${
          hiringStatusLabels[
            activeHiringProcess.status
          ]
        }.`
      : "Adayın belgeleri onaylandı ve işe alım süreci başlatılabilir."}
</Alert>

    {canVerifyDocuments && (
      <Box>
        <CreateHiringProcessDialog
          candidateId={candidate.id}
        />
      </Box>
    )}
  </Stack>
)}
            </CardContent>
          </Card>
        )}
      </Container>
    </Box>
  );
}