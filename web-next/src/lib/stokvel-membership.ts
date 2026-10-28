import type { HomeDashboard, HomeStokvel } from "./types";

export function findStokvelMembership(
  home: HomeDashboard,
  stokvelId: string,
): HomeStokvel | null {
  return (
    home.stokvels.find(
      (stokvel) =>
        stokvel.stokvelId.toLowerCase() === stokvelId.toLowerCase(),
    ) ?? null
  );
}
