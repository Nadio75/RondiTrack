import { test, expect } from "@playwright/test";

test.describe("Unauthenticated route protection", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  const protectedRoutes = [
    "/stokvels",
    "/dashboard/member",
    "/dashboard/treasurer",
    "/join",
  ];

  for (const route of protectedRoutes) {
    test(`redirects ${route} to login`, async ({ page }) => {
      await page.goto(route);

      await expect(page).toHaveURL(/\/login(?:\?|$)/);

      const currentUrl = new URL(page.url());

      expect(currentUrl.searchParams.get("next")).toBe(route);
    });
  }
});
