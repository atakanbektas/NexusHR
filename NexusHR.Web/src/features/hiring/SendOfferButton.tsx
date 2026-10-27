import {
  useState,
} from "react";

import {
  Alert,
  Button,
  CircularProgress,
  Stack,
} from "@mui/material";

import {
  hasAnyRole,
  NexusHrRoles,
} from "../../auth/roles";

import {
  sendHiringOffer,
} from "./hiringService";

interface SendOfferButtonProps {
  hiringProcessId: string;
  onSent: () => Promise<void>;
}

export default function SendOfferButton({
  hiringProcessId,
  onSent,
}: SendOfferButtonProps) {
  const [isSending, setIsSending] =
    useState(false);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  const canSendOffer =
    hasAnyRole([
      NexusHrRoles.HrSpecialist,
    ]);

  async function handleSend() {
    const isConfirmed = window.confirm(
      "İş teklifini adaya göndermek istediğinize emin misiniz? Gönderilen teklif artık düzenlenemez.",
    );

    if (!isConfirmed) {
      return;
    }

    setIsSending(true);
    setErrorMessage(null);

    try {
      await sendHiringOffer(
        hiringProcessId,
      );

      await onSent();
    } catch {
      setErrorMessage(
        "İş teklifi gönderilemedi. Teklifin hazırlanmış durumda olduğunu kontrol edin.",
      );
    } finally {
      setIsSending(false);
    }
  }

  if (!canSendOffer) {
    return null;
  }

  return (
    <Stack spacing={1}>
      {errorMessage && (
        <Alert severity="error">
          {errorMessage}
        </Alert>
      )}

      <Button
        variant="contained"
        color="success"
        disabled={isSending}
        onClick={() => {
          void handleSend();
        }}
        startIcon={
          isSending
            ? (
              <CircularProgress
                size={18}
                color="inherit"
              />
            )
            : undefined
        }
      >
        {isSending
          ? "Gönderiliyor"
          : "Teklifi gönder"}
      </Button>
    </Stack>
  );
}