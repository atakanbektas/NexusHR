import {
  publicApi,
} from "../../api/publicApi";

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
  await publicApi.post(
    "/api/public/offers/respond",
    request,
  );
}
