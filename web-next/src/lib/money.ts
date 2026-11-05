export const MAX_CONTRIBUTION_CENTS = 2147483647;

const allowedAmount = /^(?:0|[1-9]\d*)(?:[.,]\d{1,2})?$/;

export function parseRandsToCents(input: string): number | null {
  if (typeof input !== "string") return null;

  let value = input.trim();

  value = value.replace(/^R\s*/i, "");

  // Accept normal and non-breaking spaces between thousands.
  value = value.replace(/[\s\u00A0\u202F]/g, "");

  if (!allowedAmount.test(value)) return null;

  // Never accept ambiguous mixed separators.
  if (value.includes(",") && value.includes(".")) return null;

  const normalized = value.replace(",", ".");
  const [rands, fraction = ""] = normalized.split(".");

  const cents =
    BigInt(rands) * BigInt(100) +
    BigInt(fraction.padEnd(2, "0"));

  if (cents < BigInt(0) || cents > BigInt(MAX_CONTRIBUTION_CENTS)) {
    return null;
  }

  return Number(cents);
}

export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError("Cents must be a non-negative safe integer.");
  }

  const rands = Math.floor(cents / 100);
  const fraction = String(cents % 100).padStart(2, "0");

  return `${rands},${fraction}`;
}

