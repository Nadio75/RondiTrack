import { z } from "zod";
import { parseRandsToCents } from "./money";

export const contributionSchema = z.object({
  amount: z
    .string()
    .trim()
    .refine(
      (value) => {
        const cents = parseRandsToCents(value);
        return cents !== null && cents > 0;
      },
      "Enter a valid amount greater than zero and within the allowed limit.",
    ),
});

export function toCents(amount: string): number {
  const cents = parseRandsToCents(amount);

  if (cents === null) {
    throw new RangeError("Invalid contribution amount.");
  }

  return cents;
}
