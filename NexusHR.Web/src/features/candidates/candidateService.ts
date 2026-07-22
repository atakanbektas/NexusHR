import { candidateApi } from "../../api/candidateApi";
import type {
  CandidateDetail,
  CreateCandidateRequest,
  CreateCandidateResponse,
  ExtractedCandidateDraft,
  GetCandidatesResponse,
} from "./candidateTypes";

export async function extractCandidateFromCv(
  file: File,
): Promise<ExtractedCandidateDraft> {
  const formData = new FormData();

  formData.append("file", file);

  const response =
    await candidateApi.post<ExtractedCandidateDraft>(
      "/api/candidates/extract-cv",
      formData,
    );

  return response.data;
}

export async function createCandidate(
  request: CreateCandidateRequest,
): Promise<CreateCandidateResponse> {
  const response =
    await candidateApi.post<CreateCandidateResponse>(
      "/api/candidates",
      request,
    );

  return response.data;
}

export async function uploadCandidateCv(
  candidateId: string,
  file: File,
): Promise<void> {
  const formData = new FormData();

  formData.append("file", file);

  await candidateApi.post(
    `/api/candidates/${candidateId}/documents/cv`,
    formData,
  );
}

export async function getCandidates(
  page: number,
  pageSize: number,
): Promise<GetCandidatesResponse> {
  const response =
    await candidateApi.get<GetCandidatesResponse>(
      "/api/candidates",
      {
        params: {
          page,
          pageSize,
        },
      },
    );

  return response.data;
}

export async function getCandidateById(
  candidateId: string,
): Promise<CandidateDetail> {
  const response =
    await candidateApi.get<CandidateDetail>(
      `/api/candidates/${candidateId}`,
    );

  return response.data;
}

export async function downloadCandidateCv(
  candidateId: string,
  fileName: string,
): Promise<void> {
  const response = await candidateApi.get<Blob>(
    `/api/candidates/${candidateId}/documents/cv`,
    {
      responseType: "blob",
    },
  );

  const downloadUrl = window.URL.createObjectURL(
    response.data,
  );

  const link = document.createElement("a");

  link.href = downloadUrl;
  link.download = fileName;

  document.body.appendChild(link);

  link.click();
  link.remove();

  window.URL.revokeObjectURL(downloadUrl);
}

export async function verifyCandidateDocuments(
  candidateId: string,
): Promise<void> {
  await candidateApi.post(
    `/api/candidates/${candidateId}/documents/verify`,
  );
}