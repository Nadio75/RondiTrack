import type { Session } from "./session-crypto";

type RefreshResult = Session | null;

type Entry = {
  promise: Promise<RefreshResult>;
  expiresAt: number;
};

const flights = new Map<string, Entry>();

const RESULT_GRACE_MS = 5000;

export function singleFlightRefresh(
  session: Session,
  refresh: () => Promise<RefreshResult>,
): Promise<RefreshResult> {
  const key = session.refreshToken;
  const existing = flights.get(key);

  if (existing && existing.expiresAt > Date.now()) {
    return existing.promise;
  }

  const entry: Entry = {
    promise: Promise.resolve(null),
    expiresAt: Number.POSITIVE_INFINITY,
  };

  const promise = Promise.resolve()
    .then(refresh)
    .then(
      (result) => {
        entry.expiresAt = Date.now() + RESULT_GRACE_MS;
        return result;
      },
      (error) => {
        if (flights.get(key) === entry) {
          flights.delete(key);
        }
        throw error;
      },
    );

  entry.promise = promise;
  flights.set(key, entry);

  return promise;
}
