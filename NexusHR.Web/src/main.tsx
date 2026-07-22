import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App";
import keycloak from "./auth/keycloak";

async function bootstrap() {
  try {
    const authenticated =
      await keycloak.init({
        onLoad: "login-required",
        pkceMethod: "S256",
        checkLoginIframe: false,
      });

    if (!authenticated) {
      await keycloak.login();
      return;
    }

    createRoot(
      document.getElementById("root")!,
    ).render(
      <StrictMode>
        <App />
      </StrictMode>,
    );
  } catch (error) {
    console.error(
      "Keycloak başlatılamadı.",
      error,
    );

    const root =
      document.getElementById("root");

    if (root) {
      root.innerHTML = `
        <div style="
          font-family: Arial, sans-serif;
          padding: 48px;
          text-align: center;
        ">
          <h2>Oturum başlatılamadı</h2>
          <p>Identity servisine ulaşılamadı.</p>
          <button onclick="window.location.reload()">
            Tekrar dene
          </button>
        </div>
      `;
    }
  }
}

void bootstrap();