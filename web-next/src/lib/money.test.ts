import { describe, expect, it } from "vitest";
import {
  MAX_CONTRIBUTION_CENTS,
  formatCents,
  parseRandsToCents,
} from "./money";

describe("South African money parsing", () => {
  it.each([
    ["1500", 150000],
    ["1 500", 150000],
    ["1 500,50", 150050],
    ["R1500", 150000],
    ["R 1 500,50", 150050],
    ["1\u00A0500,50", 150050],
    ["1\u202F500,50", 150050],
    ["1,5", 150],
    ["19,99", 1999],
    ["0,01", 1],
    ["150.50", 15050],
  ])("parses %s into %i cents", (input, expected) => {
    expect(parseRandsToCents(input)).toBe(expected);
  });

  it.each([
    "",
    " ",
    "-100",
    "abc",
    "12,345",
    "1,500.50",
    "R nonsense",
    "21474836,48",
  ])("rejects invalid amount %s", (input) => {
    expect(parseRandsToCents(input)).toBeNull();
  });

  it.each([
    0,
    1,
    99,
    100,
    150050,
    MAX_CONTRIBUTION_CENTS,
  ])("round-trips %i cents exactly", (cents) => {
    expect(parseRandsToCents(formatCents(cents))).toBe(cents);
  });

  it("never loses a cent through floating-point arithmetic", () => {
    expect(parseRandsToCents("19,99")).toBe(1999);
  });
});
