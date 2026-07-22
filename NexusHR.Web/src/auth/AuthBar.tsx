import {
  AppBar,
  Box,
  Button,
  Chip,
  Stack,
  Toolbar,
  Typography,
} from "@mui/material";
import {
  Logout,
  Security,
} from "@mui/icons-material";
import keycloak from "./keycloak";

export default function AuthBar() {
  const username =
    keycloak.tokenParsed?.preferred_username ??
    "Kullanıcı";

  const roles =
    keycloak.realmAccess?.roles.filter(
      role =>
        !role.startsWith("default-roles") &&
        role !== "offline_access" &&
        role !== "uma_authorization",
    ) ?? [];

  async function handleLogout() {
    await keycloak.logout({
      redirectUri: window.location.origin,
    });
  }

  return (
    <AppBar
      position="sticky"
      elevation={0}
      color="inherit"
      sx={{
        borderBottom: "1px solid",
        borderColor: "divider",
      }}
    >
      <Toolbar>
        <Security
          color="primary"
          sx={{ mr: 1.5 }}
        />

        <Typography
          variant="h6"
          sx={{
            fontWeight: 800,
            mr: 3,
          }}
        >
          NexusHR
        </Typography>

        <Stack
          direction="row"
          spacing={1}
          sx={{
            flexGrow: 1,
            alignItems: "center",
          }}
        >
          {roles.map(role => (
            <Chip
              key={role}
              label={role}
              size="small"
              color="primary"
              variant="outlined"
            />
          ))}
        </Stack>

        <Box sx={{ textAlign: "right", mr: 2 }}>
          <Typography
            variant="body2"
            sx={{ fontWeight: 700 }}
          >
            {username}
          </Typography>

          <Typography
            variant="caption"
            color="text.secondary"
          >
            Oturum açık
          </Typography>
        </Box>

        <Button
          color="inherit"
          startIcon={<Logout />}
          onClick={handleLogout}
        >
          Çıkış
        </Button>
      </Toolbar>
    </AppBar>
  );
}