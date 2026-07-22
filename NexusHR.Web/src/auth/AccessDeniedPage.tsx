import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Stack,
  Typography,
} from "@mui/material";
import {
  Logout,
  Lock,
} from "@mui/icons-material";
import keycloak from "./keycloak";

export default function AccessDeniedPage() {
  async function handleLogout() {
    await keycloak.logout({
      redirectUri: window.location.origin,
    });
  }

  return (
    <Box
      sx={{
        minHeight: "calc(100vh - 64px)",
        display: "grid",
        placeItems: "center",
        background:
          "linear-gradient(135deg, #f4f7fb 0%, #eef2ff 100%)",
        py: 4,
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
          <CardContent sx={{ p: 5 }}>
            <Stack
              spacing={3}
              sx={{
                alignItems: "center",
                textAlign: "center",
              }}
            >
              <Lock
                color="error"
                sx={{ fontSize: 64 }}
              />

              <Typography
                variant="h2"
                sx={{ fontWeight: 800 }}
              >
                403
              </Typography>

              <Typography
                variant="h5"
                sx={{ fontWeight: 700 }}
              >
                Bu sayfaya erişiminiz yok
              </Typography>

              <Alert severity="warning">
                Kullanıcı rolünüz bu işlemi
                gerçekleştirmek için yeterli değil.
              </Alert>

              <Button
                variant="outlined"
                startIcon={<Logout />}
                onClick={handleLogout}
              >
                Farklı kullanıcıyla giriş yap
              </Button>
            </Stack>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}