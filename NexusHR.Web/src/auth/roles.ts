import keycloak from "./keycloak";

export const NexusHrRoles = {
  HrSpecialist: "HrSpecialist",
  HrManager: "HrManager",
  DepartmentManager:
    "DepartmentManager",
  ItSpecialist: "ItSpecialist",
  FinanceSpecialist:
    "FinanceSpecialist",
  Admin: "Admin",
  Auditor: "Auditor",
} as const;

export const CandidateReaderRoles = [
  NexusHrRoles.HrSpecialist,
  NexusHrRoles.HrManager,
  NexusHrRoles.DepartmentManager,
  NexusHrRoles.Auditor,
] as const;

export const CandidateCvReaderRoles = [
  NexusHrRoles.HrSpecialist,
  NexusHrRoles.HrManager,
  NexusHrRoles.Auditor,
] as const;

export function hasAnyRole(
  roles: readonly string[],
): boolean {
  return roles.some(role =>
    keycloak.hasRealmRole(role),
  );
}