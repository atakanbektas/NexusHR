import { candidateApi } from "../../api/candidateApi";
import type {
  CandidateOverviewResponse,
  CandidateOverviewStatusFilter,
} from "./candidateOverviewTypes";

export async function getCandidateOverview(
  page: number,
  pageSize: number,
  search?: string,
  status?: CandidateOverviewStatusFilter,
): Promise<CandidateOverviewResponse> {
  const response =
    await candidateApi.get<CandidateOverviewResponse>(
      "/api/candidate-overview",
      {
        params: {
          page,
          pageSize,
          search: search?.trim() || undefined,
          status: status || undefined,
        },
      },
    );

  return response.data;
}
