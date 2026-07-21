import axios from "axios";

export const candidateApi = axios.create({
  baseURL: import.meta.env.VITE_CANDIDATE_API_URL,
  headers: {
    Accept: "application/json",
  },
});