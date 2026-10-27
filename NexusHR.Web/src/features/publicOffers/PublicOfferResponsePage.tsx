import {
  useState,
} from "react";

import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Container,
  Stack,
  TextField,
  Typography,
} from "@mui/material";

import {
  CheckCircle,
  Close,
} from "@mui/icons-material";

import {
  useSearchParams,
} from "react-router-dom";

import {
  respondToOffer,
} from "./publicOfferService";

import type {
  PublicOfferDecision,
} from "./publicOfferService";

interface ApiErrorResponse {
  errors?: string[];
}

export default function PublicOfferResponsePage() {
  const [searchParams] =
    useSearchParams();

  const token =
    searchParams.get("token")?.trim() ?? "";

  const [
    rejectionReason,
    setRejectionReason,
  ] = useState("");

  const [
    submittingDecision,
    setSubmittingDecision,
  ] = useState<
    PublicOfferDecision | null
  >(null);

  const [
    completedDecision,
    setCompletedDecision,
  ] = useState<
    PublicOfferDecision | null
  >(null);

  const [
    errorMessage,
    setErrorMessage,
  ] = useState<string | null>(null);

  async function handleRespond(
    decision: PublicOfferDecision,
  ) {
    const normalizedReason =
      rejectionReason.trim();

    if (
      decision === "Reject" &&
      !normalizedReason
    ) {
      setErrorMessage(
        "Teklifi reddetmek için ret nedeni yazmalısınız.",
      );

      return;
    }

    const confirmationMessage =
      decision === "Accept"
        ? "İş teklifini kabul etmek istediğinize emin misiniz?"
        : "İş teklifini reddetmek istediğinize emin misiniz?";

    if (
      !window.confirm(
        confirmationMessage,
      )
    ) {
      return;
    }

    setSubmittingDecision(decision);
    setErrorMessage(null);

    try {
      await respondToOffer({
        token,
        decision,
        rejectionReason:
          decision === "Reject"
            ? normalizedReason
            : null,
      });

      setCompletedDecision(decision);
    } catch (error) {
      const apiError =
        error as {
          response?: {
            data?: ApiErrorResponse;
            status?: number;
          };
        };

      const apiMessage =
        apiError.response?.data
          ?.errors?.[0];

      setErrorMessage(
        apiMessage ??
          "Teklif cevabınız kaydedilemedi. Bağlantı geçersiz, kullanılmış veya süresi dolmuş olabilir.",
      );
    } finally {
      setSubmittingDecision(null);
    }
  }

  const isSubmitting =
    submittingDecision !== null;

  return (
    <Box
      sx={{
        minHeight: "100vh",
        background:
          "linear-gradient(135deg, #f4f7fb 0%, #eef2ff 100%)",
        py: {
          xs: 4,
          md: 10,
        },
      }}
    >
      <Container maxWidth="sm">
        <Card
          elevation={0}
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 4,
          }}
        >
          <CardContent
            sx={{
              p: {
                xs: 3,
                md: 5,
              },
            }}
          >
            <Typography
              variant="h4"
              sx={{
                fontWeight: 800,
                mb: 1,
              }}
            >
              NexusHR İş Teklifi
            </Typography>

            <Typography
              color="text.secondary"
              sx={{ mb: 4 }}
            >
              İş teklifimize ilişkin kararınızı
              bu sayfadan iletebilirsiniz.
            </Typography>

            {!token && (
              <Alert severity="error">
                Teklif bağlantısı geçersiz.
                E-postanızdaki bağlantıyı
                tekrar kontrol edin.
              </Alert>
            )}

            {token &&
              completedDecision ===
                "Accept" && (
                <Alert
                  severity="success"
                  icon={<CheckCircle />}
                >
                  İş teklifini kabul ettiğiniz
                  için teşekkür ederiz. İnsan
                  kaynakları ekibimiz sizinle
                  iletişime geçecektir.
                </Alert>
              )}

            {token &&
              completedDecision ===
                "Reject" && (
                <Alert
                  severity="info"
                  icon={<Close />}
                >
                  Teklif cevabınız kaydedildi.
                  İlginiz ve ayırdığınız zaman
                  için teşekkür ederiz.
                </Alert>
              )}

            {token &&
              completedDecision === null && (
                <Stack spacing={3}>
                  {errorMessage && (
                    <Alert severity="error">
                      {errorMessage}
                    </Alert>
                  )}

                  <Alert severity="info">
                    Kararınız kaydedildikten sonra
                    bu bağlantı yeniden
                    kullanılamaz.
                  </Alert>

                  <TextField
                    label="Ret nedeni"
                    placeholder="Teklifi reddedecekseniz nedenini yazın."
                    value={rejectionReason}
                    onChange={event =>
                      setRejectionReason(
                        event.target.value,
                      )
                    }
                    multiline
                    minRows={3}
                    fullWidth
                    disabled={isSubmitting}
                    slotProps={{
                      htmlInput: {
                        maxLength: 1000,
                      },
                    }}
                  />

                  <Stack
                    direction={{
                      xs: "column",
                      sm: "row",
                    }}
                    spacing={2}
                  >
                    <Button
                      variant="contained"
                      color="success"
                      fullWidth
                      disabled={isSubmitting}
                      startIcon={
                        submittingDecision ===
                        "Accept"
                          ? (
                              <CircularProgress
                                size={18}
                                color="inherit"
                              />
                            )
                          : <CheckCircle />
                      }
                      onClick={() => {
                        void handleRespond(
                          "Accept",
                        );
                      }}
                    >
                      {submittingDecision ===
                      "Accept"
                        ? "Kaydediliyor"
                        : "Teklifi kabul et"}
                    </Button>

                    <Button
                      variant="outlined"
                      color="error"
                      fullWidth
                      disabled={isSubmitting}
                      startIcon={
                        submittingDecision ===
                        "Reject"
                          ? (
                              <CircularProgress
                                size={18}
                                color="inherit"
                              />
                            )
                          : <Close />
                      }
                      onClick={() => {
                        void handleRespond(
                          "Reject",
                        );
                      }}
                    >
                      {submittingDecision ===
                      "Reject"
                        ? "Kaydediliyor"
                        : "Teklifi reddet"}
                    </Button>
                  </Stack>
                </Stack>
              )}
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}