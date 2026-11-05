import { test, expect } from "@playwright/test";

test("homepage redirects visitors to the appropriate page", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveURL(
    /\/(stokvels|login)(?:[/?#]|$)/,
  );

  await expect(page.locator("body")).toBeVisible();
});
