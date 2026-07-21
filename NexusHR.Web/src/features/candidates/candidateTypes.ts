export interface ExtractedCandidateDraft {
  firstName: string | null;
  lastName: string | null;
  email: string | null;
  phoneNumber: string | null;
}

export interface CreateCandidateRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
}

export interface CreateCandidateResponse {
  id: string;
}