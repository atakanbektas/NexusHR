import {
  AppBar,
  Box,
  Button,
  Toolbar,
  Typography,
} from "@mui/material";
import {
  BrowserRouter,
  Link as RouterLink,
  Navigate,
  Route,
  Routes,
} from "react-router-dom";
import CandidateDetailPage from "./features/candidates/CandidateDetailPage";
import CandidateListPage from "./features/candidates/CandidateListPage";
import CreateCandidateFromCvPage from "./features/candidates/CreateCandidateFromCvPage";

function App() {
  return (
    <BrowserRouter>
      <Box sx={{ minHeight: "100vh", bgcolor: "#f4f7fb" }}>
        <AppBar position="static" elevation={0}>
          <Toolbar sx={{ gap: 1 }}>
            <Typography
              variant="h6"
              component={RouterLink}
              to="/candidates"
              sx={{
                color: "inherit",
                textDecoration: "none",
                fontWeight: 800,
                flexGrow: 1,
              }}
            >
              NexusHR
            </Typography>

            <Button
              color="inherit"
              component={RouterLink}
              to="/candidates"
            >
              Adaylar
            </Button>

            <Button
              color="inherit"
              component={RouterLink}
              to="/candidates/new"
            >
              Yeni aday
            </Button>
          </Toolbar>
        </AppBar>

        <Routes>
          <Route
            path="/"
            element={<Navigate to="/candidates" replace />}
          />
          <Route path="/candidates" element={<CandidateListPage />} />
          <Route
            path="/candidates/new"
            element={<CreateCandidateFromCvPage />}
          />
          <Route
            path="/candidates/:id"
            element={<CandidateDetailPage />}
          />
          <Route
            path="*"
            element={<Navigate to="/candidates" replace />}
          />
        </Routes>
      </Box>
    </BrowserRouter>
  );
}

export default App;
