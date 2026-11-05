import { beforeEach, describe, expect, it, vi } from "vitest";
import { randomUUID } from "node:crypto";

vi.mock("node:crypto", () => ({
  randomUUID: vi.fn(() => "generated-payment-key"),
}));

vi.mock("next/cache", () => ({
  revalidatePath: vi.fn(),
}));

vi.mock("@/lib/api", () => {
  class ApiError extends Error {
    constructor(
      public readonly status: number,
      message: string,
      public readonly correlationId: string | null = null,
      public readonly retryAfter: number | null = null,
    ) {
      super(message);
    }
  }

  return {
    ApiError,
    apiRequest: vi.fn(),
  };
});

vi.mock("@/lib/session", () => ({
  getSession: vi.fn(),
}));

import { ApiError, apiRequest } from "@/lib/api";
import { getSession } from "@/lib/session";
import {
  recordContribution,
  type ContributionActionState,
} from "./actions";

const stokvelId = "11111111-1111-4111-8111-111111111111";
const cycleId = "22222222-2222-4222-8222-222222222222";
const userId = "33333333-3333-4333-8333-333333333333";

const initialState: ContributionActionState = {
  status: "idle",
  message: "",
};

function payment(amount: string) {
  const data = new FormData();
  data.set("amount", amount);
  return data;
}

describe("recordContribution", () => {
  beforeEach(() => {
    vi.mocked(getSession).mockResolvedValue({
      user: { id: userId },
      accessToken: "test-token",
    } as Awaited<ReturnType<typeof getSession>>);

    vi.mocked(apiRequest).mockResolvedValue(undefined);
  });

  it("accepts R150,50 and sends 15050 cents", async () => {
    const result = await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("R150,50"),
    );

    expect(result.status).toBe("success");

    expect(apiRequest).toHaveBeenCalledWith(
      expect.stringContaining("/contributions"),
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          "Idempotency-Key": "generated-payment-key",
        }),
        body: JSON.stringify({
          userId,
          amountInCents: 15050,
        }),
      }),
    );
  });

  it("rejects an invalid amount without calling the API", async () => {
    const result = await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("12,345"),
    );

    expect(result.status).toBe("error");
    expect(apiRequest).not.toHaveBeenCalled();
  });

  it("returns the API reference for a 409 conflict", async () => {
    vi.mocked(apiRequest).mockRejectedValue(
      new ApiError(409, "Conflict", "REF-409"),
    );

    const result = await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("150,50"),
    );

    expect(result.status).toBe("error");
    expect(result.correlationId).toBe("REF-409");
    expect(result.retryKey).toBe("generated-payment-key");
  });

  it("reuses the idempotency key when retrying the same payment", async () => {
    vi.mocked(apiRequest)
      .mockRejectedValueOnce(new Error("Network unavailable"))
      .mockResolvedValueOnce(undefined);

    const first = await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("150,50"),
    );

    expect(first.status).toBe("error");

    const second = await recordContribution(
      stokvelId,
      cycleId,
      first,
      payment("150,50"),
    );

    expect(second.status).toBe("success");

    const firstOptions = vi.mocked(apiRequest).mock.calls[0][1];
    const secondOptions = vi.mocked(apiRequest).mock.calls[1][1];

    expect(
      (firstOptions?.headers as Record<string, string>)["Idempotency-Key"],
    ).toBe(
      (secondOptions?.headers as Record<string, string>)["Idempotency-Key"],
    );
  });

  it("generates a new key for a separate payment", async () => {
    vi.mocked(randomUUID)
      .mockReturnValueOnce("payment-key-one" as ReturnType<typeof randomUUID>)
      .mockReturnValueOnce("payment-key-two" as ReturnType<typeof randomUUID>);

    await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("150,50"),
    );

    await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("200,00"),
    );

    const firstOptions = vi.mocked(apiRequest).mock.calls[0][1];
    const secondOptions = vi.mocked(apiRequest).mock.calls[1][1];

    const firstKey =
      (firstOptions?.headers as Record<string, string>)["Idempotency-Key"];

    const secondKey =
      (secondOptions?.headers as Record<string, string>)["Idempotency-Key"];

    expect(firstKey).toBe("payment-key-one");
    expect(secondKey).toBe("payment-key-two");
    expect(firstKey).not.toBe(secondKey);
  });

  it("preserves the entered amount for invalid input", async () => {
    const result = await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("12,345"),
    );

    expect(result.status).toBe("error");
    expect(result.submittedAmount).toBe("12,345");
    expect(result.fieldErrors?.amount).toBeTruthy();
    expect(apiRequest).not.toHaveBeenCalled();
  });

  it("preserves the amount and reference after an API conflict", async () => {
    vi.mocked(apiRequest).mockRejectedValueOnce(
      new ApiError(409, "Conflict", "REF-409"),
    );

    const result = await recordContribution(
      stokvelId,
      cycleId,
      initialState,
      payment("150,50"),
    );

    expect(result.status).toBe("error");
    expect(result.submittedAmount).toBe("150,50");
    expect(result.correlationId).toBe("REF-409");
  });
});
