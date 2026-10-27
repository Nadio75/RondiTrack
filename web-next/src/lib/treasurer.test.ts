import { describe, expect, it } from "vitest";
import { stokvelsWhereTreasurer } from "./treasurer";
import type { HomeStokvel } from "./types";

function stokvel(id: string, role: string): HomeStokvel {
  return {
    stokvelId: id,
    name: `Stokvel ${id}`,
    role,
    contributionAmount: "R100",
    contributionAmountCents: 10000,
    personalArrearsCents: 0,
    currentCycle: null,
  };
}

describe("stokvelsWhereTreasurer", () => {
  it("returns only Treasurer stokvels from mixed roles", () => {
    const result = stokvelsWhereTreasurer([
      stokvel("one", "Member"),
      stokvel("two", "Treasurer"),
      stokvel("three", "Admin"),
      stokvel("four", "Treasurer"),
    ]);

    expect(result.map((item) => item.stokvelId)).toEqual([
      "two",
      "four",
    ]);
  });

  it("returns an empty array when there are no Treasurer roles", () => {
    expect(
      stokvelsWhereTreasurer([
        stokvel("one", "Member"),
        stokvel("two", "Admin"),
      ]),
    ).toEqual([]);
  });

  it("returns an empty array when there are no stokvels", () => {
    expect(stokvelsWhereTreasurer([])).toEqual([]);
  });

  it("requires the exact Treasurer role", () => {
    expect(
      stokvelsWhereTreasurer([
        stokvel("one", "treasurer"),
        stokvel("two", "Treasurer"),
      ]).map((item) => item.stokvelId),
    ).toEqual(["two"]);
  });
});
