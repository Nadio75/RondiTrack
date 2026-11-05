export type PaymentStatus =
  | "Not paid"
  | "Partially paid"
  | "Paid in full";

export function getPaymentStatus(
  paidCents: number,
  owedCents: number,
): PaymentStatus {
  if (paidCents <= 0) {
    return "Not paid";
  }

  if (owedCents > 0) {
    return "Partially paid";
  }

  return "Paid in full";
}
