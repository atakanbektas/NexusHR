import {
  candidateApi,
} from "../../api/candidateApi";

import type {
  CreateHiringProcessRequest,
  CreateHiringProcessResponse,
  HiringProcessDetail,
  PrepareOfferRequest,
  ActiveHiringProcessLookup
} from "./hiringTypes";

export async function createHiringProcess(
  request: CreateHiringProcessRequest,
): Promise<CreateHiringProcessResponse> {
  const response =
    await candidateApi.post<CreateHiringProcessResponse>(
      "/api/hiring-processes",
      request,
    );

  return response.data;
}

export async function getHiringProcessById(
  hiringProcessId: string,
): Promise<HiringProcessDetail> {
  const response =
    await candidateApi.get<HiringProcessDetail>(
      `/api/hiring-processes/${hiringProcessId}`,
    );

  return response.data;
}

export async function getActiveHiringProcessByCandidateId(
  candidateId: string,
): Promise<ActiveHiringProcessLookup> {
  const response =
    await candidateApi.get<ActiveHiringProcessLookup>(
      `/api/hiring-processes/active/by-candidate/${candidateId}`,
    );

  return response.data;
}

export async function prepareHiringOffer(
  hiringProcessId: string,
  request: PrepareOfferRequest,
): Promise<void> {
  await candidateApi.put(
    `/api/hiring-processes/${hiringProcessId}/offer`,
    request,
  );
}

export async function sendHiringOffer(
  hiringProcessId: string,
): Promise<void> {
  await candidateApi.post(
    `/api/hiring-processes/${hiringProcessId}/offer/send`,
  );
}