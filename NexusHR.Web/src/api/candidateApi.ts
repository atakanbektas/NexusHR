import axios from "axios";
import keycloak from "../auth/keycloak";

export const candidateApi = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: {
    Accept: "application/json",
  },
});

candidateApi.interceptors.request.use(
  async config => {
    if (keycloak.authenticated) {
      try {
        await keycloak.updateToken(30);
      } catch {
        await keycloak.login();

        return config;
      }

      config.headers.Authorization =
        `Bearer ${keycloak.token}`;
    }

    return config;
  },
);