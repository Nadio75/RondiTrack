import "server-only";

export function getServerTime(): number {
  return performance.now();
}
