import { test, expect } from "@playwright/test";

test("login form validates required fields", async ({ page }) => {
  await page.goto("/login");

  const email = page.getByRole("textbox", { name: "Email" });
  const password = page.getByLabel("Password");
  const signIn = page.getByRole("button", { name: "Sign in" });

  await expect(email).toBeVisible();
  await expect(password).toBeVisible();
  await expect(signIn).toBeVisible();

  await signIn.click();

  const emailIsMissing = await email.evaluate(
    (element: HTMLInputElement) => element.validity.valueMissing
  );

  expect(emailIsMissing).toBe(true);

  await expect(page).toHaveURL(/\/login(?:\?|$)/);
});
