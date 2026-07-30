import {
  useState,
} from "react";

import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from "@mui/material";

import {
  hasAnyRole,
  NexusHrRoles,
} from "../../auth/roles";

import {
  prepareHiringOffer,
} from "./hiringService";

interface PrepareOfferDialogProps {
  hiringProcessId: string;
  isEditing?: boolean;
  onPrepared: () => Promise<void>;
}

export default function PrepareOfferDialog({
  hiringProcessId,
  isEditing = false,
  onPrepared,
}: PrepareOfferDialogProps) {
  const [isOpen, setIsOpen] =
    useState(false);

  const [grossSalary, setGrossSalary] =
    useState("");

  const [currency, setCurrency] =
    useState("TRY");

  const [
    proposedStartDate,
    setProposedStartDate,
  ] = useState("");

  const [
    offerExpiresAtLocal,
    setOfferExpiresAtLocal,
  ] = useState("");

  const [isSubmitting, setIsSubmitting] =
    useState(false);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  const canPrepareOffer =
    hasAnyRole([
      NexusHrRoles.HrSpecialist,
    ]);

  function handleOpen() {
    setErrorMessage(null);
    setIsOpen(true);
  }

  function handleClose() {
    if (!isSubmitting) {
      setIsOpen(false);
    }
  }

  async function handleSubmit() {
    const normalizedSalary =
      Number(grossSalary);

    if (
      !Number.isFinite(normalizedSalary) ||
      normalizedSalary <= 0
    ) {
      setErrorMessage(
        "Brüt maaş sıfırdan büyük olmalıdır.",
      );

      return;
    }

    if (!proposedStartDate) {
      setErrorMessage(
        "Önerilen başlangıç tarihi seçilmelidir.",
      );

      return;
    }

    if (!offerExpiresAtLocal) {
      setErrorMessage(
        "Teklif son geçerlilik zamanı seçilmelidir.",
      );

      return;
    }

    const offerExpiration =
      new Date(offerExpiresAtLocal);

    if (
      Number.isNaN(
        offerExpiration.getTime(),
      ) ||
      offerExpiration <= new Date()
    ) {
      setErrorMessage(
        "Teklif son geçerlilik zamanı gelecekte olmalıdır.",
      );

      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      await prepareHiringOffer(
        hiringProcessId,
        {
          grossSalary:
            normalizedSalary,
          currency,
          proposedStartDate,
          offerExpiresAtUtc:
            offerExpiration.toISOString(),
        },
      );

      await onPrepared();

      setIsOpen(false);
    } catch {
      setErrorMessage(
        "İş teklifi hazırlanamadı. Bilgileri ve sürecin mevcut durumunu kontrol edin.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!canPrepareOffer) {
    return null;
  }

  return (
    <>
      <Button
        variant="contained"
        onClick={handleOpen}
      >
        {isEditing
          ? "Teklifi düzenle"
          : "İş teklifini hazırla"}
      </Button>

      <Dialog
        open={isOpen}
        onClose={handleClose}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>
          {isEditing
            ? "İş teklifini düzenle"
            : "İş teklifini hazırla"}
        </DialogTitle>

        <DialogContent>
          <Stack
            spacing={3}
            sx={{ pt: 1 }}
          >
            {errorMessage && (
              <Alert severity="error">
                {errorMessage}
              </Alert>
            )}

            <TextField
              label="Aylık brüt maaş"
              type="number"
              value={grossSalary}
              onChange={event =>
                setGrossSalary(
                  event.target.value,
                )
              }
              slotProps={{
                htmlInput: {
                  min: 1,
                  step: 0.01,
                },
              }}
              required
              fullWidth
              disabled={isSubmitting}
            />

            <TextField
              select
              label="Para birimi"
              value={currency}
              onChange={event =>
                setCurrency(
                  event.target.value,
                )
              }
              required
              fullWidth
              disabled={isSubmitting}
            >
              <MenuItem value="TRY">
                Türk lirası — TRY
              </MenuItem>

              <MenuItem value="USD">
                Amerikan doları — USD
              </MenuItem>

              <MenuItem value="EUR">
                Euro — EUR
              </MenuItem>
            </TextField>

            <TextField
              label="Önerilen başlangıç tarihi"
              type="date"
              value={proposedStartDate}
              onChange={event =>
                setProposedStartDate(
                  event.target.value,
                )
              }
              slotProps={{
                inputLabel: {
                  shrink: true,
                },
              }}
              required
              fullWidth
              disabled={isSubmitting}
            />

            <TextField
              label="Teklif son geçerlilik zamanı"
              type="datetime-local"
              value={offerExpiresAtLocal}
              onChange={event =>
                setOfferExpiresAtLocal(
                  event.target.value,
                )
              }
              slotProps={{
                inputLabel: {
                  shrink: true,
                },
              }}
              required
              fullWidth
              disabled={isSubmitting}
            />
          </Stack>
        </DialogContent>

        <DialogActions
          sx={{ px: 3, pb: 3 }}
        >
          <Button
            onClick={handleClose}
            disabled={isSubmitting}
          >
            Vazgeç
          </Button>

          <Button
            variant="contained"
            disabled={isSubmitting}
            onClick={() => {
              void handleSubmit();
            }}
            startIcon={
              isSubmitting
                ? (
                  <CircularProgress
                    size={18}
                    color="inherit"
                  />
                )
                : undefined
            }
          >
            {isSubmitting
              ? "Kaydediliyor"
              : "Teklifi kaydet"}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}