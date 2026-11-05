import { describe, expect, it } from "vitest";
import { getPaymentStatus } from "./payment-status";

describe("getPaymentStatus", () => {
  it("shows Not paid when no contribution exists", () => {
    expect(getPaymentStatus(0, 50000)).toBe("Not paid");
  });

  it("shows Partially paid after a smaller contribution", () => {
    expect(getPaymentStatus(15050, 34950)).toBe(
      "Partially paid",
    );
  });

  it("shows Paid in full when nothing remains owed", () => {
    expect(getPaymentStatus(50000, 0)).toBe(
      "Paid in full",
    );
  });

  it("shows Paid in full for an overpayment", () => {
    expect(getPaymentStatus(60000, 0)).toBe(
      "Paid in full",
    );
  });
});
