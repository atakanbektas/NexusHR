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
  MenuItem,
  Pagination,
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
import { Add, Search } from "@mui/icons-material";
import { Link as RouterLink } from "react-router-dom";
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

export default function CandidateListPage() {
  const [items, setItems] = useState<CandidateListItem[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<CandidateStatus | "">("");
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isActive = true;

    const timer = window.setTimeout(async () => {
      setIsLoading(true);
      setErrorMessage(null);

      try {
        const response = await getCandidates({
          page,
          pageSize,
          search: search.trim() || undefined,
          status: status || undefined,
        });

        if (isActive) {
          setItems(response.items);
          setTotalCount(response.totalCount);
        }
      } catch {
        if (isActive) {
          setErrorMessage("Adaylar yüklenirken bir hata oluştu.");
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    }, 300);

    return () => {
      isActive = false;
      window.clearTimeout(timer);
    };
  }, [page, search, status]);

  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <Container maxWidth="lg" sx={{ py: { xs: 3, md: 6 } }}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ mb: 3, justifyContent: "space-between" }}
      >
        <Box>
          <Typography variant="h4" sx={{ fontWeight: 800 }}>
            Adaylar
          </Typography>
          <Typography color="text.secondary">
            Adayları arayın, durumlarına göre filtreleyin ve detaylarını inceleyin.
          </Typography>
        </Box>

        <Button
          variant="contained"
          startIcon={<Add />}
          component={RouterLink}
          to="/candidates/new"
          sx={{ alignSelf: { sm: "center" } }}
        >
          Yeni aday
        </Button>
      </Stack>

      <Card elevation={0} sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent>
          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={2}
            sx={{ mb: 3 }}
          >
            <TextField
              fullWidth
              label="Aday ara"
              placeholder="Ad, soyad, e-posta veya telefon"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
              slotProps={{
                input: {
                  startAdornment: <Search color="action" sx={{ mr: 1 }} />,
                },
              }}
            />

            <TextField
              select
              label="Durum"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value as CandidateStatus | "");
                setPage(1);
              }}
              sx={{ minWidth: { md: 220 } }}
            >
              <MenuItem value="">Tüm durumlar</MenuItem>
              {candidateStatuses.map((candidateStatus) => (
                <MenuItem key={candidateStatus} value={candidateStatus}>
                  {candidateStatusLabels[candidateStatus]}
                </MenuItem>
              ))}
            </TextField>
          </Stack>

          {errorMessage && <Alert severity="error">{errorMessage}</Alert>}

          {isLoading ? (
            <Box sx={{ display: "grid", placeItems: "center", py: 8 }}>
              <CircularProgress />
            </Box>
          ) : items.length === 0 ? (
            <Box sx={{ textAlign: "center", py: 8 }}>
              <Typography variant="h6">Aday bulunamadı</Typography>
              <Typography color="text.secondary">
                Arama veya durum filtrenizi değiştirebilirsiniz.
              </Typography>
            </Box>
          ) : (
            <>
              <TableContainer>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>Aday</TableCell>
                      <TableCell>İletişim</TableCell>
                      <TableCell>Durum</TableCell>
                      <TableCell>Kayıt tarihi</TableCell>
                      <TableCell align="right">İşlem</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {items.map((candidate) => (
                      <TableRow key={candidate.id} hover>
                        <TableCell>
                          <Typography sx={{ fontWeight: 700 }}>
                            {candidate.firstName} {candidate.lastName}
                          </Typography>
                          <Typography variant="body2" color="text.secondary">
                            {candidate.email}
                          </Typography>
                        </TableCell>
                        <TableCell>{candidate.phoneNumber}</TableCell>
                        <TableCell>
                          <Chip
                            size="small"
                            label={candidateStatusLabels[candidate.status]}
                            color={candidateStatusColors[candidate.status]}
                          />
                        </TableCell>
                        <TableCell>
                          {new Date(candidate.createdAtUtc).toLocaleDateString("tr-TR")}
                        </TableCell>
                        <TableCell align="right">
                          <Button
                            component={RouterLink}
                            to={`/candidates/${candidate.id}`}
                          >
                            Detay
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>

              <Stack sx={{ mt: 3, alignItems: "center" }}>
                <Pagination
                  page={page}
                  count={pageCount}
                  color="primary"
                  onChange={(_, value) => setPage(value)}
                />
              </Stack>
            </>
          )}
        </CardContent>
      </Card>
    </Container>
  );
}
