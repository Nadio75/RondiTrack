import { test, expect, request } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const API = "http://127.0.0.1:5057";

function loadCredentials(): Record<string, string> {
  const file = resolve(process.cwd(), "../.e2e-test-users.local");
  const credentials: Record<string, string> = {};

  for (const line of readFileSync(file, "utf8").split(/\r?\n/)) {
    const separator = line.indexOf("=");

    if (separator > 0) {
      credentials[line.slice(0, separator)] =
        line.slice(separator + 1).trim();
    }
  }

  return credentials;
}

async function login(
  api: Awaited<ReturnType<typeof request.newContext>>,
  email: string,
  password: string
): Promise<string> {
  const response = await api.post(`${API}/api/v1/auth/login`, {
    data: { email, password },
  });

  expect(response.status()).toBe(200);

  const body = await response.json();
  expect(body.accessToken).toBeTruthy();

  return body.accessToken;
}

test.describe("Cycle closing authorization", () => {
  test("Treasurer gets 200, Member gets 403, visitor gets 401", async () => {
    const credentials = loadCredentials();

    const stokvelId = credentials.E2E_STOKVEL_ID;
    const cycleId = credentials.E2E_CYCLE_ID;

    expect(stokvelId).toBeTruthy();
    expect(cycleId).toBeTruthy();

    const url =
      `${API}/api/v1/stokvels/${stokvelId}` +
      `/cycles/${cycleId}/closing-preview`;

    const api = await request.newContext();

    try {
      const treasurerToken = await login(
        api,
        credentials.E2E_TREASURER_EMAIL,
        credentials.E2E_TREASURER_PASSWORD
      );

      const treasurerResponse = await api.get(url, {
        headers: {
          Authorization: `Bearer ${treasurerToken}`,
        },
      });

      expect(treasurerResponse.status()).toBe(200);

      const memberToken = await login(
        api,
        credentials.E2E_MEMBER_EMAIL,
        credentials.E2E_MEMBER_PASSWORD
      );

      const memberResponse = await api.get(url, {
        headers: {
          Authorization: `Bearer ${memberToken}`,
        },
      });

      expect(memberResponse.status()).toBe(403);

      const visitorResponse = await api.get(url);

      expect(visitorResponse.status()).toBe(401);
    } finally {
      await api.dispose();
    }
  });
});
