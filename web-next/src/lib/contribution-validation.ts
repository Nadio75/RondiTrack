import { z } from "zod";

export const contributionSchema = z.object({
  amount: z
    .string()
    .trim()
    .regex(
      /^(?:0|[1-9]\d*)(?:\.\d{1,2})?$/,
      "Enter a valid amount with no more than two decimal places.",
    )
    .refine(
      (value) => {
        const [rands, cents = ""] = value.split(".");
        const amountInCents =
          BigInt(rands) * BigInt(100) + BigInt(cents.padEnd(2, "0"));

        return amountInCents > BigInt(0) &&
          amountInCents <= BigInt(2147483647);
      },
      "Amount must be greater than zero and within the allowed limit.",
    ),
});

export function toCents(amount: string): number {
  const [rands, cents = ""] = amount.split(".");

  return Number(
    BigInt(rands) * BigInt(100) + BigInt(cents.padEnd(2, "0")),
  );
}

