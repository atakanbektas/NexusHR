import type {
  CandidateStatus,
} from "./candidateTypes";
import type {
  HiringProcessStatus,
} from "../hiring/hiringTypes";

export type CandidateOverviewStatusSource =
  | "Candidate"
  | "Hiring";

export type CandidateOverviewStatusFilter =
  | `candidate:${CandidateStatus}`
  | `hiring:${HiringProcessStatus}`;

export interface CandidateOverviewItem {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  candidateStatus: CandidateStatus;
  hiringProcessId: string | null;
  hiringStatus: HiringProcessStatus | null;
  displayStatusSource: CandidateOverviewStatusSource;
  displayStatus: CandidateStatus | HiringProcessStatus;
  createdAtUtc: string;
}

export interface CandidateOverviewResponse {
  items: CandidateOverviewItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}
