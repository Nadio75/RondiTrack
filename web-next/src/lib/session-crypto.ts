import { EncryptJWT, jwtDecrypt } from "jose";

export type Session = {
  accessToken: string;
  refreshToken: string;
  user: {
    id: string;
    name: string;
    email: string;
  };
};

export const sessionCookieName = "session";

export const sessionCookieOptions = {
  httpOnly: true,
  secure: true,
  sameSite: "lax" as const,
  path: "/",
  maxAge: 60 * 60 * 24 * 7,
};

function encryptionKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET;

  if (!secret) {
    throw new Error("SESSION_SECRET is missing.");
  }

  const key = Buffer.from(secret, "base64");

  if (key.length !== 32) {
    throw new Error("SESSION_SECRET must decode to 32 bytes.");
  }

  return key;
}

export async function encryptSession(session: Session): Promise<string> {
  return new EncryptJWT({
    accessToken: session.accessToken,
    refreshToken: session.refreshToken,
    user: session.user,
  })
    .setProtectedHeader({ alg: "dir", enc: "A256GCM" })
    .setIssuedAt()
    .setExpirationTime("7d")
    .encrypt(encryptionKey());
}

export async function decryptSession(
  value: string | undefined,
): Promise<Session | null> {
  if (!value) return null;

  try {
    const { payload } = await jwtDecrypt(value, encryptionKey(), {
      keyManagementAlgorithms: ["dir"],
      contentEncryptionAlgorithms: ["A256GCM"],
    });

    if (
      typeof payload.accessToken !== "string" ||
      typeof payload.refreshToken !== "string" ||
      !payload.user ||
      typeof payload.user !== "object"
    ) {
      return null;
    }

    const user = payload.user as Record<string, unknown>;

    if (
      typeof user.id !== "string" ||
      typeof user.name !== "string" ||
      typeof user.email !== "string"
    ) {
      return null;
    }

    return {
      accessToken: payload.accessToken,
      refreshToken: payload.refreshToken,
      user: {
        id: user.id,
        name: user.name,
        email: user.email,
      },
    };
  } catch {
    return null;
  }
}
