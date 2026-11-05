import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("login page has no detectable WCAG A/AA violations", async ({ page }) => {
  await page.goto("/login");

  await expect(
    page.getByRole("button", { name: "Sign in" }),
  ).toBeVisible();

  const results = await new AxeBuilder({ page })
    .withTags([
      "wcag2a",
      "wcag2aa",
      "wcag21a",
      "wcag21aa",
      "wcag22aa",
    ])
    .analyze();

  expect(results.violations).toEqual([]);
});
