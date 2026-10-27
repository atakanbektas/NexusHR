import {
  useEffect,
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
  ArrowForward,
  WorkOutlined,
} from "@mui/icons-material";

import {
  useNavigate,
} from "react-router-dom";

import {
  createHiringProcess,
  getActiveHiringProcessByCandidateId,
} from "./hiringService";

import type {
  EmploymentType,
} from "./hiringTypes";

interface CreateHiringProcessDialogProps {
  candidateId: string;
}

const employmentTypeOptions: Array<{
  value: EmploymentType;
  label: string;
}> = [
  { value: 1, label: "Tam zamanlı" },
  { value: 2, label: "Yarı zamanlı" },
  { value: 3, label: "Belirli süreli" },
  { value: 4, label: "Stajyer" },
  { value: 5, label: "Danışman / yüklenici" },
];

export default function CreateHiringProcessDialog({
  candidateId,
}: CreateHiringProcessDialogProps) {
  const navigate = useNavigate();

  const [isOpen, setIsOpen] =
    useState(false);

  const [positionTitle, setPositionTitle] =
    useState("");

  const [department, setDepartment] =
    useState("");

  const [employmentType, setEmploymentType] =
    useState<EmploymentType>(1);

  const [activeHiringProcessId, setActiveHiringProcessId] =
    useState<string | null>(null);

  const [isCheckingProcess, setIsCheckingProcess] =
    useState(true);

  const [isSubmitting, setIsSubmitting] =
    useState(false);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  useEffect(() => {
    let isActive = true;

    async function checkActiveHiringProcess() {
      try {
        const response =
          await getActiveHiringProcessByCandidateId(
            candidateId,
          );

        if (
          isActive &&
          response.exists &&
          response.hiringProcessId
        ) {
          setActiveHiringProcessId(
            response.hiringProcessId,
          );
        }
      } catch {
        if (isActive) {
          setErrorMessage(
            "Adayın işe alım süreci kontrol edilemedi.",
          );
        }
      } finally {
        if (isActive) {
          setIsCheckingProcess(false);
        }
      }
    }

    void checkActiveHiringProcess();

    return () => {
      isActive = false;
    };
  }, [candidateId]);

  function handleMainButtonClick() {
    if (activeHiringProcessId) {
      navigate(
        `/hiring-processes/${activeHiringProcessId}`,
      );

      return;
    }

    setErrorMessage(null);
    setIsOpen(true);
  }

  function handleClose() {
    if (!isSubmitting) {
      setIsOpen(false);
    }
  }

  async function handleSubmit() {
    const normalizedPositionTitle =
      positionTitle.trim();

    const normalizedDepartment =
      department.trim();

    if (!normalizedPositionTitle) {
      setErrorMessage(
        "Pozisyon adı boş bırakılamaz.",
      );

      return;
    }

    if (!normalizedDepartment) {
      setErrorMessage(
        "Departman boş bırakılamaz.",
      );

      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      const response =
        await createHiringProcess({
          candidateId,
          positionTitle: normalizedPositionTitle,
          department: normalizedDepartment,
          employmentType,
        });

      setActiveHiringProcessId(response.id);
      setIsOpen(false);

      navigate(
        `/hiring-processes/${response.id}`,
      );
    } catch {
      setErrorMessage(
        "İşe alım süreci oluşturulamadı. Aday için aktif bir süreç bulunuyor olabilir.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <Button
        variant="contained"
        startIcon={
          isCheckingProcess
            ? <CircularProgress size={18} color="inherit" />
            : activeHiringProcessId
              ? <ArrowForward />
              : <WorkOutlined />
        }
        disabled={isCheckingProcess}
        onClick={handleMainButtonClick}
      >
        {isCheckingProcess
          ? "Süreç kontrol ediliyor"
          : activeHiringProcessId
            ? "İşe alım sürecine git"
            : "İşe alım süreci başlat"}
      </Button>

      <Dialog
        open={isOpen}
        onClose={handleClose}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>
          İşe alım süreci başlat
        </DialogTitle>

        <DialogContent>
          <Stack spacing={3} sx={{ pt: 1 }}>
            {errorMessage && (
              <Alert severity="error">
                {errorMessage}
              </Alert>
            )}

            <TextField
              label="Pozisyon"
              value={positionTitle}
              onChange={event =>
                setPositionTitle(event.target.value)
              }
              required
              fullWidth
              disabled={isSubmitting}
            />

            <TextField
              label="Departman"
              value={department}
              onChange={event =>
                setDepartment(event.target.value)
              }
              required
              fullWidth
              disabled={isSubmitting}
            />

            <TextField
              select
              label="Çalışma tipi"
              value={employmentType}
              onChange={event =>
                setEmploymentType(
                  Number(
                    event.target.value,
                  ) as EmploymentType,
                )
              }
              fullWidth
              disabled={isSubmitting}
            >
              {employmentTypeOptions.map(
                option => (
                  <MenuItem
                    key={option.value}
                    value={option.value}
                  >
                    {option.label}
                  </MenuItem>
                ),
              )}
            </TextField>
          </Stack>
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 3 }}>
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
                : <WorkOutlined />
            }
          >
            {isSubmitting
              ? "Oluşturuluyor"
              : "Süreci oluştur"}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}