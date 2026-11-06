import { test, expect } from '@playwright/test';

test('post a review for a product and see it in the review list', async ({ page }) => {
  // Reviews persist between runs, so tag this one to find it among earlier reviews.
  const title = `Trail tested ${Date.now()}`;

  await page.goto('/');
  await page.getByRole('link', { name: 'Adventurer GPS Watch' }).click();
  await expect(page.getByRole('heading', { name: 'Adventurer GPS Watch' })).toBeVisible();

  const reviews = page.getByRole('region', { name: 'Customer reviews' });
  await expect(reviews).toBeVisible();

  // Submitting without a rating fails form validation before anything reaches the API.
  await reviews.getByRole('button', { name: 'Post review' }).click();
  await expect(reviews.getByText('Choose a star rating.')).toBeVisible();

  await reviews.getByRole('radio', { name: '4 stars' }).check({ force: true });
  await reviews.getByRole('textbox', { name: /^Title/ }).fill(title);
  await reviews.getByRole('textbox', { name: /^Review/ }).fill('Held a satellite fix the whole way up the ridge.');
  await reviews.getByRole('button', { name: 'Post review' }).click();

  await expect(reviews.getByRole('status')).toHaveText('Thanks! Your review has been posted.');
  await expect(reviews.getByText(/Based on \d+ reviews?/)).toBeVisible();

  const posted = reviews.getByRole('listitem').filter({ has: page.getByRole('heading', { name: title }) });
  await expect(posted).toBeVisible();
  await expect(posted.getByRole('img', { name: 'Rated 4 out of 5' })).toBeVisible();
  await expect(posted).toContainText('Held a satellite fix the whole way up the ridge.');
});

test('anonymous shoppers are asked to log in before reviewing', async ({ browser }) => {
  const context = await browser.newContext({ storageState: { cookies: [], origins: [] } });
  const page = await context.newPage();

  await page.goto('/');
  await page.getByRole('link', { name: 'Adventurer GPS Watch' }).click();

  const reviews = page.getByRole('region', { name: 'Customer reviews' });
  await expect(reviews.getByRole('link', { name: 'Log in' })).toBeVisible();
  await expect(reviews.getByRole('button', { name: 'Post review' })).toHaveCount(0);

  await context.close();
});
