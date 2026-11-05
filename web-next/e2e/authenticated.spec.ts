import { test, expect } from "@playwright/test";
import { loginAs } from "./auth-helper";

test("Chairperson can log in and view their stokvel", async ({ page }) => {
  await loginAs(page, "CHAIR");

  await expect(
    page.getByText("E2E Savings Group"),
  ).toBeVisible();

  await expect(
    page.getByText("Contribution: 500.00"),
  ).toBeVisible();
});

test("ordinary Member can log in", async ({ page }) => {
  await loginAs(page, "MEMBER");

  await expect(page).toHaveURL(/\/stokvels(?:\/|\?|$)/);

  await expect(
    page.getByText("E2E Savings Group"),
  ).toBeVisible();
});

