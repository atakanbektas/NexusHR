export type CandidateStatus =
  | "Draft"
  | "DocumentsPending"
  | "ReadyForHiring"
  | "Archived";

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

export interface CandidateListItem {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  status: CandidateStatus;
  createdAtUtc: string;
}

export interface GetCandidatesResponse {
  items: CandidateListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CandidateDetail {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  status: CandidateStatus;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}