export type EmploymentType =
  | 1
  | 2
  | 3
  | 4
  | 5;

export type HiringProcessStatus =
  | "Draft"
  | "OfferPrepared"
  | "OfferSent"
  | "OfferAccepted"
  | "OfferRejected"
  | "Cancelled"
  | "Completed";

export interface CreateHiringProcessRequest {
  candidateId: string;
  positionTitle: string;
  department: string;
  employmentType: EmploymentType;
}

export interface CreateHiringProcessResponse {
  id: string;
}

export interface HiringProcessDetail {
  id: string;
  candidateId: string;
  candidateFullName: string;
  candidateEmail: string;
  employeeId: string | null;
  positionTitle: string;
  department: string;
  employmentType: string;
  status: HiringProcessStatus;
  grossSalary: number | null;
  currency: string | null;
  proposedStartDate: string | null;
  offerExpiresAtUtc: string | null;
  offerSentAtUtc: string | null;
  offerRespondedAtUtc: string | null;
  rejectionReason: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface ActiveHiringProcessLookup {
  exists: boolean;
  hiringProcessId: string | null;
  status: HiringProcessStatus | null;
}


export interface PrepareOfferRequest {
  grossSalary: number;
  currency: string;
  proposedStartDate: string;
  offerExpiresAtUtc: string;
}