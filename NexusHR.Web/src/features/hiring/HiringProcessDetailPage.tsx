import {
  useEffect,
  useState,
} from "react";

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
} from "@mui/icons-material";

import {
  Link as RouterLink,
  useParams,
} from "react-router-dom";

import {
  getHiringProcessById,
} from "./hiringService";

import type {
  HiringProcessDetail,
  HiringProcessStatus,
} from "./hiringTypes";

import PrepareOfferDialog from
  "./PrepareOfferDialog";

  import SendOfferButton from
  "./SendOfferButton";

const statusLabels:
Record<HiringProcessStatus, string> = {
  Draft: "Taslak",
  OfferPrepared: "Teklif hazırlandı",
  OfferSent: "Teklif gönderildi",
  OfferAccepted: "Teklif kabul edildi",
  OfferRejected: "Teklif reddedildi",
  Cancelled: "İptal edildi",
  Completed: "Tamamlandı",
};

const employmentTypeLabels:
Record<string, string> = {
  FullTime: "Tam zamanlı",
  PartTime: "Yarı zamanlı",
  FixedTerm: "Belirli süreli",
  Intern: "Stajyer",
  Contractor: "Danışman / yüklenici",
};

export default function HiringProcessDetailPage() {
  const { id } = useParams();

  const [hiringProcess, setHiringProcess] =
    useState<HiringProcessDetail | null>(
      null,
    );

  const [isLoading, setIsLoading] =
    useState(true);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      setIsLoading(false);
      return;
    }

    let isActive = true;

    async function loadHiringProcess() {
      try {
        const response =
          await getHiringProcessById(id!);

        if (isActive) {
          setHiringProcess(response);
        }
      } catch {
        if (isActive) {
          setErrorMessage(
            "İşe alım süreci yüklenemedi.",
          );
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    }

    void loadHiringProcess();

    return () => {
      isActive = false;
    };
  }, [id]);

  async function reloadHiringProcess() {
    if (!id) {
      return;
    }

    const response =
      await getHiringProcessById(id);

    setHiringProcess(response);
  }

  if (!id) {
    return (
      <Container
        maxWidth="md"
        sx={{ py: 6 }}
      >
        <Alert severity="error">
          İşe alım süreci kimliği bulunamadı.
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

        {hiringProcess && (
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
                  justifyContent:
                    "space-between",
                  mb: 3,
                }}
              >
                <Box>
                  <Typography
                    variant="h4"
                    sx={{ fontWeight: 800 }}
                  >
                    {
                      hiringProcess
                        .positionTitle
                    }
                  </Typography>

                  <Typography
                    color="text.secondary"
                  >
                    {
                      hiringProcess
                        .candidateFullName
                    }
                  </Typography>
                </Box>

                <Chip
                  color="primary"
                  label={
                    statusLabels[
                      hiringProcess.status
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
                <DetailItem
                  label="Aday"
                  value={
                    hiringProcess
                      .candidateFullName
                  }
                />

                <DetailItem
                  label="E-posta"
                  value={
                    hiringProcess
                      .candidateEmail
                  }
                />

                <DetailItem
                  label="Departman"
                  value={
                    hiringProcess.department
                  }
                />

                <DetailItem
                  label="Çalışma tipi"
                  value={
                    employmentTypeLabels[
                      hiringProcess
                        .employmentType
                    ] ??
                    hiringProcess
                      .employmentType
                  }
                />

                <DetailItem
                  label="Oluşturulma tarihi"
                  value={
                    new Date(
                      hiringProcess
                        .createdAtUtc,
                    ).toLocaleString(
                      "tr-TR",
                    )
                  }
                />

                <DetailItem
                  label="Başlangıç tarihi"
                  value={
                    hiringProcess
                      .proposedStartDate ??
                    "Henüz belirlenmedi"
                  }
                />
              </Box>

              {hiringProcess.grossSalary !==
                null && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Typography
                    variant="h6"
                    sx={{
                      fontWeight: 700,
                      mb: 2,
                    }}
                  >
                    Teklif bilgileri
                  </Typography>

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
                    <DetailItem
                      label="Aylık brüt maaş"
                      value={
                        new Intl.NumberFormat(
                          "tr-TR",
                          {
                            style:
                              "currency",
                            currency:
                              hiringProcess
                                .currency ??
                              "TRY",
                          },
                        ).format(
                          hiringProcess
                            .grossSalary,
                        )
                      }
                    />

                    <DetailItem
                      label="Teklif geçerlilik zamanı"
                      value={
                        hiringProcess
                          .offerExpiresAtUtc
                          ? new Date(
                              hiringProcess
                                .offerExpiresAtUtc,
                            ).toLocaleString(
                              "tr-TR",
                            )
                          : "Henüz belirlenmedi"
                      }
                    />
                  </Box>
                </>
              )}

              {(
                hiringProcess.status ===
                  "Draft" ||
                hiringProcess.status ===
                  "OfferPrepared"
              ) && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Stack spacing={2}>
                    <Alert
                      severity={
                        hiringProcess.status ===
                        "Draft"
                          ? "info"
                          : "success"
                      }
                    >
                      {hiringProcess.status ===
                      "Draft"
                        ? "Süreç taslak durumunda. Ücret ve başlangıç bilgilerini girerek iş teklifini hazırlayın."
                        : "İş teklifi hazırlandı. Teklif gönderilmeden önce bilgileri düzenleyebilirsiniz."}
                    </Alert>

<Stack
  direction={{
    xs: "column",
    sm: "row",
  }}
  spacing={2}
>
  <Box>
    <PrepareOfferDialog
      hiringProcessId={
        hiringProcess.id
      }
      isEditing={
        hiringProcess.status ===
        "OfferPrepared"
      }
      onPrepared={
        reloadHiringProcess
      }
    />
  </Box>

  {hiringProcess.status ===
    "OfferPrepared" && (
    <Box>
      <SendOfferButton
        hiringProcessId={
          hiringProcess.id
        }
        onSent={
          reloadHiringProcess
        }
      />
    </Box>
  )}
</Stack>
                  </Stack>
                </>
              )}

              {hiringProcess.status ===
                "OfferSent" && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Alert severity="info">
                    İş teklifi adaya
                    gönderildi. Adayın kabul
                    veya ret cevabı bekleniyor.
                  </Alert>
                </>
              )}

              {hiringProcess.status ===
                "OfferAccepted" && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Alert severity="success">
                    Aday iş teklifini kabul
                    etti. İşe giriş işlemleri
                    başlatılabilir.
                  </Alert>
                </>
              )}

              {hiringProcess.status ===
                "OfferRejected" && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Alert severity="error">
                    Aday iş teklifini reddetti.
                  </Alert>
                </>
              )}

              {hiringProcess.status ===
                "Cancelled" && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Alert severity="warning">
                    İşe alım süreci iptal
                    edildi.
                  </Alert>
                </>
              )}

              {hiringProcess.status ===
                "Completed" && (
                <>
                  <Divider sx={{ my: 3 }} />

                  <Alert severity="success">
                    İşe alım süreci tamamlandı.
                    Çalışan kaydı oluşturuldu.
                  </Alert>
                </>
              )}
            </CardContent>
          </Card>
        )}
      </Container>
    </Box>
  );
}

interface DetailItemProps {
  label: string;
  value: string;
}

function DetailItem({
  label,
  value,
}: DetailItemProps) {
  return (
    <Box>
      <Typography
        variant="body2"
        color="text.secondary"
      >
        {label}
      </Typography>

      <Typography sx={{ fontWeight: 600 }}>
        {value}
      </Typography>
    </Box>
  );
}