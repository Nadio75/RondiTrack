import "server-only";

import { cookies } from "next/headers";
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

const cookieName = "session";

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

export async function getSession(): Promise<Session | null> {
  const cookieStore = await cookies();
  const value = cookieStore.get(cookieName)?.value;

  if (!value) {
    return null;
  }

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

export async function setSession(session: Session): Promise<void> {
  const token = await new EncryptJWT({
    accessToken: session.accessToken,
    refreshToken: session.refreshToken,
    user: session.user,
  })
    .setProtectedHeader({ alg: "dir", enc: "A256GCM" })
    .setIssuedAt()
    .setExpirationTime("7d")
    .encrypt(encryptionKey());

  const cookieStore = await cookies();

  cookieStore.set(cookieName, token, {
    httpOnly: true,
    secure: true,
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 7,
  });
}

export async function deleteSession(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(cookieName);
}
