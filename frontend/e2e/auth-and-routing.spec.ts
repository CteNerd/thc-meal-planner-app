import { expect, test, type Page } from '@playwright/test';

async function loginWithPlaceholderAuth(page: Page) {
  await page.goto('/login');
  await page.getByLabel('Email').fill('adult1@example.com');
  await page.getByLabel('Password').fill('password123456');
  await page.getByRole('button', { name: 'Continue' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

async function stubAuthenticatedApi(page: Page) {
  await page.route('**/api/**', async (route) => {
    const url = route.request().url();

    if (url.includes('/api/chat/history')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ conversationId: 'conv_1', messages: [] })
      });
      return;
    }

    if (url.includes('/api/meal-plans/current') || url.includes('/api/grocery-lists/current')) {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Not Found', detail: 'No active data yet.' })
      });
      return;
    }

    if (url.includes('/api/meal-plans/history')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({})
    });
  });
}

test.describe('Auth and routing smoke', () => {
  test('@smoke signs in and opens dashboard shell', async ({ page }) => {
    await stubAuthenticatedApi(page);
    await loginWithPlaceholderAuth(page);
    await expect(page.getByRole('heading', { level: 2, name: 'This week at a glance' })).toBeVisible();
  });

  test('@smoke can navigate to key authenticated routes', async ({ page }) => {
    await stubAuthenticatedApi(page);
    await loginWithPlaceholderAuth(page);

    await page.goto('/meal-plans');
    await expect(page.getByRole('heading', { level: 2, name: 'Meal Plans' })).toBeVisible();

    await page.goto('/grocery-list');
    await expect(page.getByRole('heading', { level: 2, name: 'Grocery List' })).toBeVisible();

    await page.goto('/chat');
    await expect(page).toHaveURL(/\/chat$/);
  });
});
