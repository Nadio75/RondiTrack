import { describe, expect, it } from "vitest";
import { southAfricanDaysToUtcRange } from "./south-african-dates";

describe("southAfricanDaysToUtcRange", () => {
  it("converts October 2026 to the correct UTC boundaries", () => {
    expect(
      southAfricanDaysToUtcRange("2026-10-01", "2026-10-31"),
    ).toEqual({
      fromUtc: "2026-09-30T22:00:00.000Z",
      toUtc: "2026-10-31T22:00:00.000Z",
    });
  });

  it("includes a complete single South African day", () => {
    expect(
      southAfricanDaysToUtcRange("2026-10-15", "2026-10-15"),
    ).toEqual({
      fromUtc: "2026-10-14T22:00:00.000Z",
      toUtc: "2026-10-15T22:00:00.000Z",
    });
  });

  it("supports a start date without an end date", () => {
    expect(southAfricanDaysToUtcRange("2026-10-01")).toEqual({
      fromUtc: "2026-09-30T22:00:00.000Z",
    });
  });

  it("supports an end date without a start date", () => {
    expect(southAfricanDaysToUtcRange(null, "2026-10-31")).toEqual({
      toUtc: "2026-10-31T22:00:00.000Z",
    });
  });

  it("supports no dates", () => {
    expect(southAfricanDaysToUtcRange()).toEqual({});
  });

  it.each([
    "2026-02-30",
    "2026-13-01",
    "2026-00-10",
    "not-a-date",
    "2026-1-1",
  ])("rejects invalid date %s", (value) => {
    expect(() => southAfricanDaysToUtcRange(value)).toThrow();
  });

  it("rejects reversed dates", () => {
    expect(() =>
      southAfricanDaysToUtcRange("2026-10-31", "2026-10-01"),
    ).toThrow();
  });

  it("handles leap years correctly", () => {
    expect(
      southAfricanDaysToUtcRange("2028-02-29", "2028-02-29"),
    ).toEqual({
      fromUtc: "2028-02-28T22:00:00.000Z",
      toUtc: "2028-02-29T22:00:00.000Z",
    });
  });

  it("rejects February 29 in a non-leap year", () => {
    expect(() =>
      southAfricanDaysToUtcRange("2027-02-29"),
    ).toThrow();
  });
});
