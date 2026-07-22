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
  FormControl,
  InputLabel,
  MenuItem,
  Pagination,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import {
  Add,
  Clear,
  PeopleAlt,
  Search,
  Visibility,
} from "@mui/icons-material";
import { getCandidates } from "./candidateService";
import {
  candidateStatusColors,
  candidateStatusLabels,
  candidateStatuses,
} from "./candidateStatus";
import type {
  CandidateListItem,
  CandidateStatus,
} from "./candidateTypes";

const pageSize = 10;

function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const response = error.response?.data as
      | {
          errors?: string[];
          message?: string;
        }
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
  const [candidates, setCandidates] =
    useState<CandidateListItem[]>([]);

  const [page, setPage] = useState(1);

  const [totalCount, setTotalCount] =
    useState(0);

  const [searchText, setSearchText] =
    useState("");

  const [selectedStatus, setSelectedStatus] =
    useState<CandidateStatus | "">("");

  const [appliedSearch, setAppliedSearch] =
    useState("");

  const [appliedStatus, setAppliedStatus] =
    useState<CandidateStatus | "">("");

  const [isLoading, setIsLoading] =
    useState(true);

  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    async function loadCandidates() {
      setIsLoading(true);
      setErrorMessage(null);

      try {
        const response = await getCandidates(
          page,
          pageSize,
          appliedSearch,
          appliedStatus || undefined,
        );

        if (!isCancelled) {
          setCandidates(response.items);
          setTotalCount(response.totalCount);
        }
      } catch (error) {
        if (!isCancelled) {
          setErrorMessage(
            getErrorMessage(error),
          );
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
  }, [
    page,
    appliedSearch,
    appliedStatus,
  ]);

  function handleApplyFilters() {
    setPage(1);
    setAppliedSearch(searchText.trim());
    setAppliedStatus(selectedStatus);
  }

  function handleClearFilters() {
    setSearchText("");
    setSelectedStatus("");
    setAppliedSearch("");
    setAppliedStatus("");
    setPage(1);
  }

  const totalPages = Math.ceil(
    totalCount / pageSize,
  );

  const hasActiveFilter =
    Boolean(appliedSearch) ||
    Boolean(appliedStatus);

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
              sx={{
                alignSelf: "flex-start",
              }}
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

        <Card
          elevation={0}
          sx={{
            mb: 3,
            p: 3,
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 4,
          }}
        >
          <Typography
            variant="h6"
            sx={{
              fontWeight: 700,
              mb: 2,
            }}
          >
            Adaylarda ara
          </Typography>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                md: "minmax(300px, 1fr) 260px auto auto",
              },
              gap: 2,
              alignItems: "center",
            }}
          >
            <TextField
              label="Ad, soyad, e-posta veya telefon"
              value={searchText}
              onChange={event => {
                setSearchText(
                  event.target.value,
                );
              }}
              onKeyDown={event => {
                if (event.key === "Enter") {
                  handleApplyFilters();
                }
              }}
              fullWidth
            />

            <FormControl fullWidth>
              <InputLabel id="candidate-status-label">
                Durum
              </InputLabel>

              <Select
                labelId="candidate-status-label"
                label="Durum"
                value={selectedStatus}
                onChange={event => {
                  setSelectedStatus(
                    event.target.value as
                      | CandidateStatus
                      | "",
                  );
                }}
              >
                <MenuItem value="">
                  Tüm durumlar
                </MenuItem>

                {candidateStatuses.map(
                  status => (
                    <MenuItem
                      key={status}
                      value={status}
                    >
                      {
                        candidateStatusLabels[
                          status
                        ]
                      }
                    </MenuItem>
                  ),
                )}
              </Select>
            </FormControl>

            <Button
              variant="contained"
              startIcon={<Search />}
              onClick={handleApplyFilters}
              sx={{
                height: 56,
                px: 3,
              }}
            >
              Ara
            </Button>

            <Button
              variant="outlined"
              startIcon={<Clear />}
              onClick={handleClearFilters}
              disabled={
                !searchText &&
                !selectedStatus &&
                !hasActiveFilter
              }
              sx={{
                height: 56,
                px: 3,
              }}
            >
              Temizle
            </Button>
          </Box>
        </Card>

        {errorMessage && (
          <Alert
            severity="error"
            sx={{ mb: 3 }}
          >
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
              spacing={2}
              sx={{
                minHeight: 300,
                p: 4,
                textAlign: "center",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <PeopleAlt
                sx={{
                  fontSize: 56,
                  color: "text.disabled",
                }}
              />

              <Typography variant="h6">
                {hasActiveFilter
                  ? "Filtrelere uygun aday bulunamadı"
                  : "Henüz aday bulunmuyor"}
              </Typography>

              <Typography color="text.secondary">
                {hasActiveFilter
                  ? "Arama kriterlerini değiştirerek tekrar deneyin."
                  : "İlk aday kaydını oluşturarak başlayabilirsiniz."}
              </Typography>

              {hasActiveFilter ? (
                <Button
                  variant="outlined"
                  startIcon={<Clear />}
                  onClick={handleClearFilters}
                >
                  Filtreleri temizle
                </Button>
              ) : (
                <Button
                  component={Link}
                  to="/candidates/new"
                  variant="contained"
                  startIcon={<Add />}
                >
                  İlk adayı oluştur
                </Button>
              )}
            </Stack>
          ) : (
            <>
              <Box
                sx={{
                  px: 3,
                  py: 2,
                  borderBottom: "1px solid",
                  borderColor: "divider",
                }}
              >
                <Typography color="text.secondary">
                  Toplam{" "}
                  <strong>{totalCount}</strong>{" "}
                  aday bulundu.
                </Typography>
              </Box>

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

                      <TableCell align="right">
                        <strong>İşlemler</strong>
                      </TableCell>
                    </TableRow>
                  </TableHead>

                  <TableBody>
                    {candidates.map(
                      candidate => (
                        <TableRow
                          key={candidate.id}
                          hover
                        >
                          <TableCell>
                            <Typography
                              sx={{
                                fontWeight: 650,
                              }}
                            >
                              {
                                candidate.firstName
                              }{" "}
                              {
                                candidate.lastName
                              }
                            </Typography>
                          </TableCell>

                          <TableCell>
                            {candidate.email}
                          </TableCell>

                          <TableCell>
                            {
                              candidate.phoneNumber
                            }
                          </TableCell>

                          <TableCell>
                            <Chip
                              label={
                                candidateStatusLabels[
                                  candidate.status
                                ]
                              }
                              color={
                                candidateStatusColors[
                                  candidate.status
                                ]
                              }
                              size="small"
                            />
                          </TableCell>

                          <TableCell>
                            {new Date(
                              candidate.createdAtUtc,
                            ).toLocaleString(
                              "tr-TR",
                            )}
                          </TableCell>

                          <TableCell align="right">
                            <Button
                              component={Link}
                              to={`/candidates/${candidate.id}`}
                              variant="outlined"
                              size="small"
                              startIcon={
                                <Visibility />
                              }
                            >
                              Detay
                            </Button>
                          </TableCell>
                        </TableRow>
                      ),
                    )}
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
                    onChange={(
                      _,
                      newPage,
                    ) => {
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