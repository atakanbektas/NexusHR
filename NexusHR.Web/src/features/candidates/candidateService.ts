import { candidateApi } from "../../api/candidateApi";
import type {
  CreateCandidateRequest,
  CreateCandidateResponse,
  CandidateDetail,
  ExtractedCandidateDraft,
  GetCandidatesRequest,
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

export async function getCandidates(
  request: GetCandidatesRequest,
): Promise<GetCandidatesResponse> {
  const response =
    await candidateApi.get<GetCandidatesResponse>(
      "/api/candidates",
      {
        params: {
          page: request.page,
          pageSize: request.pageSize,
          search: request.search || undefined,
          status: request.status || undefined,
        },
      },
    );

  return response.data;
}

export async function getCandidateById(
  candidateId: string,
): Promise<CandidateDetail> {
  const response = await candidateApi.get<CandidateDetail>(
    `/api/candidates/${candidateId}`,
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

export async function downloadCandidateCv(
  candidateId: string,
  fileName: string,
): Promise<void> {
  const response = await candidateApi.get<Blob>(
    `/api/candidates/${candidateId}/documents/cv`,
    { responseType: "blob" },
  );

  const downloadUrl = URL.createObjectURL(response.data);
  const anchor = document.createElement("a");
  anchor.href = downloadUrl;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(downloadUrl);
}
