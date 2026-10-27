import { candidateApi } from "../../api/candidateApi";

export interface OnboardingTask {
  id: string;
  title: string;
  assignedDepartment: string;
  status: "Pending" | "InProgress" | "Completed" | "Cancelled";
  dueDate: string;
  completedAtUtc: string | null;
}

export interface Onboarding {
  id: string;
  hiringProcessId: string;
  candidateId: string;
  firstName: string;
  lastName: string;
  fullName?: string;
  positionTitle: string;
  department: string;
  proposedStartDate: string;
  status: "Draft" | "ReadyForStart" | "Completed" | "Cancelled";
  tasks: OnboardingTask[];
}

export async function getOnboardings(signal?: AbortSignal) {
  return (await candidateApi.get<Onboarding[]>("/api/onboardings", { signal })).data;
}
export async function getOnboarding(id: string, signal?: AbortSignal) {
  return (await candidateApi.get<Onboarding>(`/api/onboardings/${id}`, { signal })).data;
}
export async function updateTask(id: string, taskId: string, status: "InProgress" | "Completed") {
  await candidateApi.put(`/api/onboardings/${id}/tasks/${taskId}/status`, {
    status: status === "InProgress" ? 2 : 3,
  });
}
export async function completeOnboarding(id: string) {
  await candidateApi.post(`/api/onboardings/${id}/complete`);
}
