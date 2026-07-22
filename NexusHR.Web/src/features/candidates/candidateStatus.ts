import type { CandidateStatus } from "./candidateTypes";

export const candidateStatuses: CandidateStatus[] = [
  "Draft",
  "DocumentsPending",
  "ReadyForHiring",
  "Archived",
];

export const candidateStatusLabels: Record<CandidateStatus, string> = {
  Draft: "Taslak",
  DocumentsPending: "Belge bekliyor",
  ReadyForHiring: "İşe girişe hazır",
  Archived: "Arşivlendi",
};

export const candidateStatusColors: Record<
  CandidateStatus,
  "default" | "warning" | "success"
> = {
  Draft: "default",
  DocumentsPending: "warning",
  ReadyForHiring: "success",
  Archived: "default",
};
