import type { HomeStokvel } from "./types";

export function stokvelsWhereTreasurer(
  stokvels: HomeStokvel[],
): HomeStokvel[] {
  return stokvels.filter((stokvel) => stokvel.role === "Treasurer");
}
