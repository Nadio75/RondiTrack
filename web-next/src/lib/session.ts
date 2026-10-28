import "server-only";

import { cookies } from "next/headers";
import {
  decryptSession,
  encryptSession,
  sessionCookieName,
  sessionCookieOptions,
} from "./session-crypto";

export type { Session } from "./session-crypto";

export async function getSession() {
  const cookieStore = await cookies();
  return decryptSession(cookieStore.get(sessionCookieName)?.value);
}

export async function setSession(
  session: import("./session-crypto").Session,
): Promise<void> {
  const token = await encryptSession(session);
  (await cookies()).set(sessionCookieName, token, sessionCookieOptions);
}

export async function deleteSession(): Promise<void> {
  (await cookies()).delete(sessionCookieName);
}
