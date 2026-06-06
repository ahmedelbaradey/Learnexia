import { test, expect } from '@playwright/test';

/**
 * Smoke runway for the frontend-e2e-tester agent.
 *
 * Intentionally locale-agnostic and selector-light — it only proves the web PWA
 * boots and the login screen mounts an input, so the harness is verifiable before
 * any story specs exist. Story flows belong in their own `<StoryID>.spec.ts`,
 * preferring getByTestId / getByRole over text (see ../../.claude/agents/frontend-e2e-tester.md).
 */

test('app boots and renders the root', async ({ page }) => {
  const response = await page.goto('/');
  expect(response?.ok()).toBeTruthy();
  // RN Web mounts the app into the document body; assert it is non-empty.
  await expect(page.locator('body')).not.toBeEmpty();
});

test('login screen mounts an input', async ({ page }) => {
  await page.goto('/login');
  // Arabic is the default locale, so avoid asserting on copy — assert on structure.
  await expect(page.getByRole('textbox').first()).toBeVisible({ timeout: 30_000 });
});
