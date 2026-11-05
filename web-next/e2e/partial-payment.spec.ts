import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { loginAs } from "./auth-helper";

test("Member sees a partial payment and outstanding balance", async ({ page }) => {
  const fixturePath = resolve(
    process.cwd(),
    "../.e2e-test-users.local",
  );

  const fixture = Object.fromEntries(
    readFileSync(fixturePath, "utf8")
      .split(/\r?\n/)
      .filter((line) => line.includes("="))
      .map((line) => {
        const index = line.indexOf("=");
        return [
          line.slice(0, index).trim(),
          line.slice(index + 1).trim(),
        ];
      }),
  );

  const stokvelId = fixture.E2E_STOKVEL_ID;
  expect(stokvelId).toBeTruthy();

  await loginAs(page, "MEMBER");

  await page.goto(`/stokvels/${stokvelId}`);

  await expect(
    page.getByRole("heading", { name: "E2E Savings Group" }),
  ).toBeVisible();

  await expect(
    page.getByText("Payment: Partially paid"),
  ).toBeVisible();

  await expect(
    page.getByText("Amount owed: 349.50"),
  ).toBeVisible();

  await expect(
    page.getByRole("heading", { name: "Record your contribution" }),
  ).toHaveCount(0);

  await expect(
    page.getByText("Amount: 150.5", { exact: true }),
  ).toBeVisible();
});
