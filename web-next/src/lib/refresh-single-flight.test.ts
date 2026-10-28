import { describe, expect, it, vi } from "vitest";
import { singleFlightRefresh } from "./refresh-single-flight";
import type { Session } from "./session-crypto";

function makeSession(refreshToken: string): Session {
  return {
    accessToken: "old-access-token",
    refreshToken,
  } as Session;
}

describe("singleFlightRefresh", () => {
  it("shares one refresh operation between three simultaneous requests", async () => {
    const session = makeSession("simultaneous-token");

    let finishRefresh!: (value: Session) => void;

    const refresh = vi.fn(
      () =>
        new Promise<Session>((resolve) => {
          finishRefresh = resolve;
        }),
    );

    const first = singleFlightRefresh(session, refresh);
    const second = singleFlightRefresh(session, refresh);
    const third = singleFlightRefresh(session, refresh);

    await Promise.resolve();

    expect(refresh).toHaveBeenCalledTimes(1);

    const updatedSession = {
      ...session,
      accessToken: "new-access-token",
      refreshToken: "new-refresh-token",
    };

    finishRefresh(updatedSession);

    const results = await Promise.all([first, second, third]);

    expect(results).toEqual([
      updatedSession,
      updatedSession,
      updatedSession,
    ]);

    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it("does not share refresh operations between different tokens", async () => {
    const firstSession = makeSession("different-token-one");
    const secondSession = makeSession("different-token-two");

    const refresh = vi.fn(async (session: Session) => ({
      ...session,
      accessToken: "updated-access-token",
    }));

    await Promise.all([
      singleFlightRefresh(firstSession, () => refresh(firstSession)),
      singleFlightRefresh(secondSession, () => refresh(secondSession)),
    ]);

    expect(refresh).toHaveBeenCalledTimes(2);
  });

  it("reuses a successful refresh result for a request arriving shortly afterward", async () => {
    const session = makeSession("grace-period-token");

    const refresh = vi.fn(async () => ({
      ...session,
      accessToken: "updated-access-token",
    }));

    const first = await singleFlightRefresh(session, refresh);
    const second = await singleFlightRefresh(session, refresh);

    expect(second).toEqual(first);
    expect(refresh).toHaveBeenCalledTimes(1);
  });
});
