import {
  candidateApi,
} from "../../api/candidateApi";

export type PublicOfferDecision =
  | "Accept"
  | "Reject";

export interface RespondToOfferRequest {
  token: string;
  decision: PublicOfferDecision;
  rejectionReason: string | null;
}

export async function respondToOffer(
  request: RespondToOfferRequest,
): Promise<void> {
  await candidateApi.post(
    "/api/public/offers/respond",
    request,
  );
}