import { describe, expect, it } from "vitest";
import {
  contributionSchema,
  toCents,
} from "./contribution-validation";

describe("Contribution amount validation", () => {
  it("accepts valid amounts", () => {
    expect(contributionSchema.safeParse({ amount: "500" }).success).toBe(true);
    expect(contributionSchema.safeParse({ amount: "150.50" }).success).toBe(true);
    expect(contributionSchema.safeParse({ amount: "0.01" }).success).toBe(true);
  });

  it("rejects zero and negative amounts", () => {
    expect(contributionSchema.safeParse({ amount: "0" }).success).toBe(false);
    expect(contributionSchema.safeParse({ amount: "-50" }).success).toBe(false);
  });

  it("rejects more than two decimal places", () => {
    expect(contributionSchema.safeParse({ amount: "12.345" }).success).toBe(false);
  });

  it("rejects amounts exceeding the backend integer limit", () => {
    expect(
      contributionSchema.safeParse({ amount: "21474836.48" }).success,
    ).toBe(false);
  });

  it("converts rands to cents exactly", () => {
    expect(toCents("500")).toBe(50000);
    expect(toCents("150.50")).toBe(15050);
    expect(toCents("0.01")).toBe(1);
  });
});
