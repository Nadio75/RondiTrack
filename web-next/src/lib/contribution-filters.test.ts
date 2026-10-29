import { describe, expect, it } from "vitest";
import { loadContributionFilters } from "./contribution-filters";

describe("contribution history URL filters", () => {
  it("uses defaults when no filters are provided", () => {
    expect(loadContributionFilters({})).toEqual({
      member: "",
      from: "",
      to: "",
      sort: "newest",
    });
  });

  it("accepts valid filters", () => {
    expect(
      loadContributionFilters({
        member: "member-123",
        from: "2026-10-01",
        to: "2026-10-31",
        sort: "oldest",
      }),
    ).toEqual({
      member: "member-123",
      from: "2026-10-01",
      to: "2026-10-31",
      sort: "oldest",
    });
  });

  it("rejects an impossible calendar date", () => {
    expect(
      loadContributionFilters({
        from: "2026-02-30",
      }).from,
    ).toBe("");
  });

  it("rejects an invalid month", () => {
    expect(
      loadContributionFilters({
        from: "2026-13-01",
      }).from,
    ).toBe("");
  });

  it("rejects an incorrectly formatted date", () => {
    expect(
      loadContributionFilters({
        from: "01-10-2026",
      }).from,
    ).toBe("");
  });

  it("uses newest when sort is invalid", () => {
    expect(
      loadContributionFilters({
        sort: "random",
      }).sort,
    ).toBe("newest");
  });

  it("accepts a single-day date range", () => {
    expect(
      loadContributionFilters({
        from: "2026-10-15",
        to: "2026-10-15",
      }),
    ).toMatchObject({
      from: "2026-10-15",
      to: "2026-10-15",
    });
  });

  it("accepts a leap-year date", () => {
    expect(
      loadContributionFilters({
        from: "2028-02-29",
      }).from,
    ).toBe("2028-02-29");
  });

  it("rejects a non-leap-year February 29", () => {
    expect(
      loadContributionFilters({
        from: "2027-02-29",
      }).from,
    ).toBe("");
  });
});
