import { describe, expect, it } from "vitest";
import { findStokvelMembership } from "./stokvel-membership";
import type { HomeDashboard } from "./types";

const home: HomeDashboard = {
  stokvels: [
    {
      stokvelId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      name: "Bitcube",
      role: "Chairperson",
      contributionAmount: "500",
      contributionAmountCents: 50000,
      personalArrearsCents: 0,
      currentCycle: null,
    },
  ],
};

describe("findStokvelMembership", () => {
  it("finds a stokvel the user belongs to", () => {
    expect(
      findStokvelMembership(
        home,
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      )?.name,
    ).toBe("Bitcube");
  });

  it("returns null for a stokvel the user does not belong to", () => {
    expect(
      findStokvelMembership(
        home,
        "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      ),
    ).toBeNull();
  });

  it("matches stokvel IDs without case sensitivity", () => {
    expect(
      findStokvelMembership(
        home,
        "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA",
      )?.role,
    ).toBe("Chairperson");
  });

  it("returns null when the user has no stokvels", () => {
    expect(
      findStokvelMembership(
        { stokvels: [] },
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      ),
    ).toBeNull();
  });
});
