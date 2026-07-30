import type {
  HiringProcessStatus,
} from "./hiringTypes";

export const hiringStatusLabels:
Record<HiringProcessStatus, string> = {
  Draft: "İşe alım taslağı",
  OfferPrepared: "Teklif hazırlandı",
  OfferSent: "Teklif cevabı bekleniyor",
  OfferAccepted: "Teklif kabul edildi",
  OfferRejected: "Teklif reddedildi",
  Cancelled: "İşe alım iptal edildi",
  Completed: "İşe alım tamamlandı",
};

export const hiringStatusColors:
Record<
  HiringProcessStatus,
  | "default"
  | "primary"
  | "info"
  | "warning"
  | "success"
  | "error"
> = {
  Draft: "default",
  OfferPrepared: "info",
  OfferSent: "warning",
  OfferAccepted: "success",
  OfferRejected: "error",
  Cancelled: "default",
  Completed: "success",
};