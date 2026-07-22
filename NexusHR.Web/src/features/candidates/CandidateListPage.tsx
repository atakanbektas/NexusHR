import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import axios from "axios";
import {
  Alert,
  Box,
  Button,
  Card,
  Chip,
  CircularProgress,
  Container,
  Pagination,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {
  Add,
  PeopleAlt,
} from "@mui/icons-material";
import { getCandidates } from "./candidateService";
import type {
  CandidateListItem,
} from "./candidateTypes";

const pageSize = 10;

function getStatusLabel(status: string): string {
  switch (status) {
    case "Draft":
      return "Taslak";

    case "DocumentsPending":
      return "Belge Kontrolü Bekliyor";

    case "ReadyForHiring":
      return "İşe Alıma Hazır";

    case "Archived":
      return "Arşivlendi";

    default:
      return status;
  }
}

function getStatusColor(
  status: string,
): "default" | "warning" | "success" {
  switch (status) {
    case "DocumentsPending":
      return "warning";

    case "ReadyForHiring":
      return "success";

    default:
      return "default";
  }
}

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

  return "Adaylar yüklenirken beklenmeyen bir hata oluştu.";
}

export default function CandidateListPage() {
  const [candidates, setCandidates] = useState<
    CandidateListItem[]
  >([]);

  const [page, setPage] = useState(1);

  const [totalCount, setTotalCount] = useState(0);

  const [isLoading, setIsLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState<
    string | null
  >(null);

  useEffect(() => {
    let isCancelled = false;

    async function loadCandidates() {
      setIsLoading(true);
      setErrorMessage(null);

      try {
        const response = await getCandidates(
          page,
          pageSize,
        );

        if (!isCancelled) {
          setCandidates(response.items);
          setTotalCount(response.totalCount);
        }
      } catch (error) {
        if (!isCancelled) {
          setErrorMessage(getErrorMessage(error));
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    }

    void loadCandidates();

    return () => {
      isCancelled = true;
    };
  }, [page]);

  const totalPages = Math.ceil(
    totalCount / pageSize,
  );

  return (
    <Box
      sx={{
        minHeight: "100vh",
        background:
          "linear-gradient(135deg, #f4f7fb 0%, #eef2ff 100%)",
        py: { xs: 3, md: 7 },
      }}
    >
      <Container maxWidth="lg">
<Stack
  direction={{
    xs: "column",
    sm: "row",
  }}
  spacing={2}
  sx={{
    mb: 4,
    justifyContent: "space-between",
    alignItems: {
      xs: "stretch",
      sm: "center",
    },
  }}
>
          <Stack spacing={1}>
            <Chip
              icon={<PeopleAlt />}
              label="Aday Yönetimi"
              color="primary"
              variant="outlined"
              sx={{ alignSelf: "flex-start" }}
            />

            <Typography
              variant="h3"
              sx={{ fontWeight: 750 }}
            >
              Adaylar
            </Typography>

            <Typography color="text.secondary">
              Sistemde bulunan adayları ve işe alım
              durumlarını görüntüleyin.
            </Typography>
          </Stack>

          <Button
            component={Link}
            to="/candidates/new"
            variant="contained"
            size="large"
            startIcon={<Add />}
          >
            Yeni aday oluştur
          </Button>
        </Stack>

        {errorMessage && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {errorMessage}
          </Alert>
        )}

        <Card
          elevation={0}
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 4,
            overflow: "hidden",
          }}
        >
          {isLoading ? (
<Stack
  spacing={2}
  sx={{
    minHeight: 300,
    p: 4,
    textAlign: "center",
    alignItems: "center",
    justifyContent: "center",
  }}
>
              <CircularProgress />

              <Typography color="text.secondary">
                Adaylar yükleniyor...
              </Typography>
            </Stack>
          ) : candidates.length === 0 ? (
<Stack
  sx={{
    p: 3,
    alignItems: "center",
  }}
>
              <PeopleAlt
                sx={{
                  fontSize: 56,
                  color: "text.disabled",
                }}
              />

              <Typography variant="h6">
                Henüz aday bulunmuyor
              </Typography>

              <Typography color="text.secondary">
                İlk aday kaydını oluşturarak
                başlayabilirsiniz.
              </Typography>

              <Button
                component={Link}
                to="/candidates/new"
                variant="contained"
                startIcon={<Add />}
              >
                İlk adayı oluştur
              </Button>
            </Stack>
          ) : (
            <>
              <TableContainer>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>
                        <strong>Ad Soyad</strong>
                      </TableCell>

                      <TableCell>
                        <strong>İletişim</strong>
                      </TableCell>

                      <TableCell>
                        <strong>Telefon</strong>
                      </TableCell>

                      <TableCell>
                        <strong>Durum</strong>
                      </TableCell>

                      <TableCell>
                        <strong>Kayıt Tarihi</strong>
                      </TableCell>
                    </TableRow>
                  </TableHead>

                  <TableBody>
                    {candidates.map(candidate => (
                      <TableRow
                        key={candidate.id}
                        hover
                      >
                        <TableCell>
                          <Typography
                            sx={{ fontWeight: 650 }}
                          >
                            {candidate.firstName}{" "}
                            {candidate.lastName}
                          </Typography>
                        </TableCell>

                        <TableCell>
                          {candidate.email}
                        </TableCell>

                        <TableCell>
                          {candidate.phoneNumber}
                        </TableCell>

                        <TableCell>
                          <Chip
                            label={getStatusLabel(
                              candidate.status,
                            )}
                            color={getStatusColor(
                              candidate.status,
                            )}
                            size="small"
                          />
                        </TableCell>

                        <TableCell>
                          {new Date(
                            candidate.createdAtUtc,
                          ).toLocaleString("tr-TR")}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>

              {totalPages > 1 && (
                <Box
                  sx={{
                    p: 3,
                    display: "flex",
                    justifyContent: "center",
                  }}
                >
                  <Pagination
                    page={page}
                    count={totalPages}
                    color="primary"
                    onChange={(_, newPage) => {
                      setPage(newPage);
                    }}
                  />
                </Box>
              )}
            </>
          )}
        </Card>
      </Container>
    </Box>
  );
}