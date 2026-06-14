const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();

  await page.goto('http://localhost:8081/login', { waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(1000);
  // Force English
  await page.evaluate(() => {
    localStorage.setItem('lx_locale', 'en');
    localStorage.removeItem('lx_locale_timestamp');
  });
  await page.reload({ waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(2500);
  await page.locator('[data-testid="login-username"]').fill('demo.parent@learnexia.com');
  await page.locator('[data-testid="login-password"]').fill('Demo!Pass1');
  await page.locator('[data-testid="login-submit"]').click();
  await page.waitForURL('**/overview', { timeout: 15000 }).catch(() => {});
  await page.waitForTimeout(2500);

  // Click child selector to open
  const selector = page.locator('[data-testid="sidebar-child-selector"]');
  if (await selector.count() > 0) {
    await selector.click();
    await page.waitForTimeout(600);
  }

  // Crop to just the sidebar area (right 260px)
  await page.screenshot({ path: '/tmp/child-switcher-en.png', clip: { x: 1020, y: 0, width: 260, height: 750 } });
  console.log('Cropped sidebar screenshot saved');

  await browser.close();
})();
