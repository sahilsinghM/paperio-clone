import { test, expect } from '@playwright/test';

test.describe('Paper.io Game', () => {
  test('start screen loads and game can be started', async ({ page }) => {
    await page.goto('/');

    const startScreen = page.locator('#start-screen');
    await expect(startScreen).toBeVisible();

    const playBtn = page.locator('#play-btn');
    await expect(playBtn).toBeVisible();
    await playBtn.click();

    await expect(startScreen).toHaveClass(/hidden/);

    const deathScreen = page.locator('#death-screen');
    await expect(deathScreen).not.toBeVisible();
  });

  test('keyboard input moves player', async ({ page }) => {
    await page.goto('/');

    page.locator('#play-btn').click();

    await page.waitForTimeout(500);

    await page.keyboard.press('ArrowRight');
    await page.waitForTimeout(200);

    await page.keyboard.press('ArrowDown');
    await page.waitForTimeout(200);

    await page.keyboard.press('ArrowLeft');
    await page.waitForTimeout(200);

    await page.keyboard.press('ArrowUp');
    await page.waitForTimeout(200);
  });

  test('WASD keys move player', async ({ page }) => {
    await page.goto('/');

    page.locator('#play-btn').click();

    await page.waitForTimeout(500);

    await page.keyboard.press('d');
    await page.waitForTimeout(200);

    await page.keyboard.press('s');
    await page.waitForTimeout(200);

    await page.keyboard.press('a');
    await page.waitForTimeout(200);

    await page.keyboard.press('w');
    await page.waitForTimeout(200);
  });

  test('canvas renders after game starts', async ({ page }) => {
    await page.goto('/');

    const canvas = page.locator('#game');
    await expect(canvas).toBeVisible();

    page.locator('#play-btn').click();

    await page.waitForTimeout(1000);

    const threeCanvas = page.locator('canvas[data-engine]');
    await expect(threeCanvas).toBeVisible();

    const canvasBounds = await threeCanvas.boundingBox();
    expect(canvasBounds).not.toBeNull();
    expect(canvasBounds.width).toBeGreaterThan(0);
    expect(canvasBounds.height).toBeGreaterThan(0);
  });

  test('death screen DOM elements exist and are wired correctly', async ({ page }) => {
    await page.goto('/');

    // Verify death screen is hidden on load
    const deathScreen = page.locator('#death-screen');
    await expect(deathScreen).toHaveClass(/hidden/);

    // Verify death screen elements exist
    await expect(page.locator('#death-killer')).toBeAttached();
    await expect(page.locator('#death-territory')).toBeAttached();
    await expect(page.locator('#death-kills')).toBeAttached();
    await expect(page.locator('#respawn-btn')).toBeAttached();
  });
});
