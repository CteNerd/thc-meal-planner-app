import { expect, test } from '@playwright/test';

test.describe('Responsive layout', () => {
  test('@smoke login page core elements are visible', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { level: 1, name: /Plan the week/i })).toBeVisible();
    await expect(page.getByRole('heading', { level: 1, name: 'Sign in to your planner' })).toBeVisible();
  });
});
