import { candidateApi } from "../../api/candidateApi";
import type {
  CreateCandidateRequest,
  CreateCandidateResponse,
  ExtractedCandidateDraft,
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