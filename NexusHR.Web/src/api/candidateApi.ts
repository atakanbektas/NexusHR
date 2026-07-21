import axios from "axios";

export const candidateApi = axios.create({
  baseURL: import.meta.env.VITE_CANDIDATE_API_URL,
  headers: {
    Accept: "application/json",
  },
});

export async function uploadCandidateCv(
  candidateId: string,
  file: File
) {
  const formData = new FormData();
  formData.append("file", file);

  const response = await candidateApi.post(
    `/api/candidates/${candidateId}/documents/cv`,
    formData
  );

  return response.data;
}