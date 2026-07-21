import { useState, type ChangeEvent } from "react";
import axios from "axios";
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
  TextField,
  Typography,
} from "@mui/material";
import {
  AutoAwesome,
  CloudUpload,
  PersonAdd,
} from "@mui/icons-material";
import {
  createCandidate,
  extractCandidateFromCv,
} from "./candidateService";
import type { CreateCandidateRequest } from "./candidateTypes";

const initialForm: CreateCandidateRequest = {
  firstName: "",
  lastName: "",
  email: "",
  phoneNumber: "",
};

function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const response = error.response?.data as
      | { errors?: string[]; message?: string }
      | undefined;

    if (response?.errors?.length) {
      return response.errors.join(" ");
    }

    if (response?.message) {
      return response.message;
    }
  }

  return "İşlem sırasında beklenmeyen bir hata oluştu.";
}

export default function CreateCandidateFromCvPage() {
  const [selectedFile, setSelectedFile] =
    useState<File | null>(null);

  const [form, setForm] =
    useState<CreateCandidateRequest>(initialForm);

  const [isExtracting, setIsExtracting] =
    useState(false);

  const [isSaving, setIsSaving] =
    useState(false);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  const [successMessage, setSuccessMessage] =
    useState<string | null>(null);

  function handleFileChange(
    event: ChangeEvent<HTMLInputElement>,
  ) {
    const file = event.target.files?.[0] ?? null;

    setSelectedFile(file);
    setErrorMessage(null);
    setSuccessMessage(null);
  }

  function handleFieldChange(
    field: keyof CreateCandidateRequest,
  ) {
    return (event: ChangeEvent<HTMLInputElement>) => {
      setForm(previous => ({
        ...previous,
        [field]: event.target.value,
      }));
    };
  }

  async function handleExtract() {
    if (!selectedFile) {
      setErrorMessage("Önce bir PDF CV seçmelisiniz.");
      return;
    }

    setIsExtracting(true);
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      const draft =
        await extractCandidateFromCv(selectedFile);

      setForm({
        firstName: draft.firstName ?? "",
        lastName: draft.lastName ?? "",
        email: draft.email ?? "",
        phoneNumber: draft.phoneNumber ?? "",
      });

      setSuccessMessage(
        "CV başarıyla analiz edildi. Bilgileri kontrol edip kaydedebilirsiniz.",
      );
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsExtracting(false);
    }
  }

  async function handleSave() {
    if (
      !form.firstName.trim() ||
      !form.lastName.trim() ||
      !form.email.trim() ||
      !form.phoneNumber.trim()
    ) {
      setErrorMessage(
        "Adayı kaydetmeden önce tüm alanları doldurmalısınız.",
      );
      return;
    }

    setIsSaving(true);
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      const response = await createCandidate({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim(),
      });

      setSuccessMessage(
        `Taslak aday başarıyla oluşturuldu. Aday ID: ${response.id}`,
      );

      setForm(initialForm);
      setSelectedFile(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Box
      sx={{
        minHeight: "100vh",
        background:
          "linear-gradient(135deg, #f4f7fb 0%, #eef2ff 100%)",
        py: { xs: 3, md: 7 },
      }}
    >
      <Container maxWidth="md">
        <Stack spacing={1} sx={{ mb: 4 }}>
          <Chip
            icon={<AutoAwesome />}
            label="Akıllı CV İşleme"
            color="primary"
            variant="outlined"
            sx={{ alignSelf: "flex-start" }}
          />

          <Typography
            variant="h3"
            color="text.primary"
            sx={{ fontWeight: 750 }}
          >
            CV’den aday oluştur
          </Typography>

          <Typography color="text.secondary">
            CV’yi yükleyin, aday bilgilerini otomatik
            çıkartalım. Kaydetmeden önce bilgileri kontrol
            edebilirsiniz.
          </Typography>
        </Stack>

        <Card
          elevation={0}
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 4,
          }}
        >
          <CardContent sx={{ p: { xs: 3, md: 5 } }}>
            <Stack spacing={4}>
              <Box>
                <Typography
                  variant="h6"
                  sx={{ fontWeight: 700 }}
                  gutterBottom
                >
                  1. CV dosyasını yükleyin
                </Typography>

                <Typography
                  color="text.secondary"
                  sx={{ mb: 2 }}
                >
                  En fazla 5 MB boyutunda PDF dosyası
                  yükleyebilirsiniz.
                </Typography>

                <Stack
                  direction={{ xs: "column", sm: "row" }}
                  spacing={2}
                  sx={{ alignItems: { sm: "center" } }}
                >
                  <Button
                    component="label"
                    variant="outlined"
                    startIcon={<CloudUpload />}
                    disabled={isExtracting}
                  >
                    PDF seç
                    <input
                      hidden
                      type="file"
                      accept="application/pdf,.pdf"
                      onChange={handleFileChange}
                    />
                  </Button>

                  <Typography
                    color={
                      selectedFile
                        ? "text.primary"
                        : "text.secondary"
                    }
                  >
                    {selectedFile
                      ? selectedFile.name
                      : "Dosya seçilmedi"}
                  </Typography>

                  <Button
                    variant="contained"
                    startIcon={
                      isExtracting
                        ? <CircularProgress
                            size={18}
                            color="inherit"
                          />
                        : <AutoAwesome />
                    }
                    disabled={!selectedFile || isExtracting}
                    onClick={handleExtract}
                  >
                    {isExtracting
                      ? "Analiz ediliyor"
                      : "CV’yi analiz et"}
                  </Button>
                </Stack>
              </Box>

              <Divider />

              <Box>
                <Typography
                  variant="h6"
                  sx={{ fontWeight: 700 }}
                  gutterBottom
                >
                  2. Aday bilgilerini kontrol edin
                </Typography>

                <Typography
                  color="text.secondary"
                  sx={{ mb: 3 }}
                >
                  Eksik veya hatalı çıkarılan alanları
                  kaydetmeden önce düzenleyin.
                </Typography>

                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: {
                      xs: "1fr",
                      sm: "1fr 1fr",
                    },
                    gap: 2.5,
                  }}
                >
                  <TextField
                    label="Ad"
                    value={form.firstName}
                    onChange={handleFieldChange("firstName")}
                    fullWidth
                    required
                  />

                  <TextField
                    label="Soyad"
                    value={form.lastName}
                    onChange={handleFieldChange("lastName")}
                    fullWidth
                    required
                  />

                  <TextField
                    label="E-posta"
                    type="email"
                    value={form.email}
                    onChange={handleFieldChange("email")}
                    fullWidth
                    required
                  />

                  <TextField
                    label="Telefon"
                    value={form.phoneNumber}
                    onChange={handleFieldChange("phoneNumber")}
                    fullWidth
                    required
                  />
                </Box>
              </Box>

              {errorMessage && (
                <Alert severity="error">
                  {errorMessage}
                </Alert>
              )}

              {successMessage && (
                <Alert severity="success">
                  {successMessage}
                </Alert>
              )}

              <Button
                variant="contained"
                size="large"
                startIcon={
                  isSaving
                    ? <CircularProgress
                        size={20}
                        color="inherit"
                      />
                    : <PersonAdd />
                }
                disabled={isSaving || isExtracting}
                onClick={handleSave}
                sx={{
                  alignSelf: { xs: "stretch", sm: "flex-end" },
                  px: 4,
                }}
              >
                {isSaving
                  ? "Kaydediliyor"
                  : "Taslak adayı kaydet"}
              </Button>
            </Stack>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}