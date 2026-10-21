
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest";

type SessionModule = typeof import("./session");

const tokenResponse = (accessToken: string) =>
  new Response(
    JSON.stringify({
      accessToken,
      tokenType: "Bearer",
      expiresIn: 900,
    }),
    {
      status: 200,
      headers: { "Content-Type": "application/json" },
    },
  );

let session: SessionModule;
let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(async () => {
  vi.resetModules();
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  session = await import("./session");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("RondiTrack session", () => {
  it("shares one refresh across five parallel 401 responses", async () => {
    let refreshCount = 0;

    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
const request = new Request(
  typeof input === "string"
    ? new URL(input, "http://localhost:8080").href
    : input,
);
      const url = new URL(request.url);

      if (url.pathname.endsWith("/auth/login")) {
        return tokenResponse("old-token");
      }

      if (url.pathname.endsWith("/auth/refresh")) {
        refreshCount++;
        await new Promise((resolve) => setTimeout(resolve, 20));
        return tokenResponse("new-token");
      }

      const authorization = request.headers.get("Authorization");

      return authorization === "Bearer new-token"
        ? new Response("ok", { status: 200 })
        : new Response("expired", { status: 401 });
    });

    await session.login("test@example.com", "password");

    const responses = await Promise.all(
      Array.from({ length: 5 }, () =>
        session.authFetch("http://localhost:8080/api/v1/protected"),
      ),
    );

    expect(responses.every((response) => response.status === 200))
      .toBe(true);
    expect(refreshCount).toBe(1);
  });

  it("can refresh again after a later token expiry", async () => {
    let refreshCount = 0;

    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
const request = new Request(
  typeof input === "string"
    ? new URL(input, "http://localhost:8080").href
    : input,
);
      const url = new URL(request.url);

      if (url.pathname.endsWith("/auth/login")) {
        return tokenResponse("old-token");
      }

      if (url.pathname.endsWith("/auth/refresh")) {
        refreshCount++;
        return tokenResponse(`new-token-${refreshCount}`);
      }

      const authorization = request.headers.get("Authorization");

      if (
        authorization === "Bearer new-token-1" ||
        authorization === "Bearer new-token-2"
      ) {
        return new Response("ok", { status: 200 });
      }

      return new Response("expired", { status: 401 });
    });

    await session.login("test@example.com", "password");

    expect(
      (await session.authFetch("http://localhost:8080/api/v1/protected"))
        .status,
    ).toBe(200);

    // Force another 401 after the first refresh.
    // Simulate a later expiry of the first refreshed token.
let expireFirstRefreshedToken = true;

fetchMock.mockImplementation(
  async (input: RequestInfo | URL) => {
    const request = new Request(
      typeof input === "string"
        ? new URL(input, "http://localhost:8080").href
        : input,
    );

    const url = new URL(request.url);

    if (url.pathname.endsWith("/auth/refresh")) {
      refreshCount++;
      return tokenResponse(`new-token-${refreshCount}`);
    }

    const authorization = request.headers.get("Authorization");

    if (
      authorization === "Bearer new-token-1" &&
      expireFirstRefreshedToken
    ) {
      expireFirstRefreshedToken = false;
      return new Response("expired", { status: 401 });
    }

    return authorization === "Bearer new-token-2"
      ? new Response("ok", { status: 200 })
      : new Response("expired", { status: 401 });
  },
);

    expect(
      (await session.authFetch("http://localhost:8080/api/v1/protected"))
        .status,
    ).toBe(200);

    expect(refreshCount).toBe(2);
  });

  it("signs out after refresh failure without retrying forever", async () => {
    let refreshCount = 0;

    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
      const request = new Request(
        typeof input === "string"
          ? new URL(input, "http://localhost:8080").href
          : input,
      );
      const url = new URL(request.url);

      if (url.pathname.endsWith("/auth/login")) {
        return tokenResponse("old-token");
      }

      if (url.pathname.endsWith("/auth/refresh")) {
        refreshCount++;
        return new Response(null, { status: 401 });
      }

      return new Response(null, { status: 401 });
    });

    await session.login("test@example.com", "password");

    await expect(
      session.authFetch("http://localhost:8080/api/v1/protected"),
    ).rejects.toThrow("session has expired");

    expect(session.getAccessToken()).toBeNull();
    expect(refreshCount).toBe(1);
  });

  it("tries one cookie refresh when no access token exists", async () => {
    let refreshCount = 0;

    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
const request = new Request(
  typeof input === "string"
    ? new URL(input, "http://localhost:8080").href
    : input,
);
      if (new URL(request.url).pathname.endsWith("/auth/refresh")) {
        refreshCount++;
        return new Response(null, { status: 401 });
      }

      return new Response(null, { status: 200 });
    });

    await expect(
      session.authFetch("http://localhost:8080/api/v1/protected"),
    ).rejects.toThrow("session has expired");

    expect(refreshCount).toBe(1);
    expect(session.getAccessToken()).toBeNull();
  });

  it("preserves a POST body when retrying after a 401", async () => {
    const bodies: string[] = [];

    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
const request = new Request(
  typeof input === "string"
    ? new URL(input, "http://localhost:8080").href
    : input,
);
      const url = new URL(request.url);

      if (url.pathname.endsWith("/auth/login")) {
        return tokenResponse("old-token");
      }

      if (url.pathname.endsWith("/auth/refresh")) {
        return tokenResponse("new-token");
      }

      bodies.push(await request.text());

      return request.headers.get("Authorization") === "Bearer new-token"
        ? new Response(null, { status: 201 })
        : new Response(null, { status: 401 });
    });

    await session.login("test@example.com", "password");

    const response = await session.authFetch(
      "http://localhost:8080/api/v1/contributions",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ amount: 250 }),
      },
    );

    expect(response.status).toBe(201);
    expect(bodies).toEqual([
      '{"amount":250}',
      '{"amount":250}',
    ]);
  });
});
