import { expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

type TestRole = "CHAIR" | "MEMBER";

function loadCredentials(): Record<string, string> {
  const path = resolve(process.cwd(), "../.e2e-test-users.local");
  const content = readFileSync(path, "utf8");
  const credentials: Record<string, string> = {};

  for (const line of content.split(/\r?\n/)) {
    const separator = line.indexOf("=");

    if (separator <= 0) continue;

    const key = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim();

    credentials[key] = value;
  }

  return credentials;
}

export async function loginAs(
  page: Page,
  role: TestRole,
): Promise<void> {
  const credentials = loadCredentials();

  const email = credentials[`E2E_${role}_EMAIL`];
  const password = credentials[`E2E_${role}_PASSWORD`];

  if (!email || !password) {
    throw new Error(`Missing E2E credentials for ${role}`);
  }

  await page.goto("/login");

  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByLabel("Password").fill(password);

  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL(/\/stokvels(?:\/|\?|$)/);
}
