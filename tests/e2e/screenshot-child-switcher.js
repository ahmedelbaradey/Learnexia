const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();

  // Login as demo parent
  await page.goto('http://localhost:8081/login', { waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(1500);
  await page.evaluate(() => { localStorage.setItem('lx_locale', 'en'); });
  await page.reload({ waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(2000);
  await page.locator('[data-testid="login-username"]').fill('demo.parent@learnexia.com');
  await page.locator('[data-testid="login-password"]').fill('Demo!Pass1');
  await page.locator('[data-testid="login-submit"]').click();
  await page.waitForURL('**/overview', { timeout: 15000 }).catch(() => {});
  await page.waitForTimeout(2500);

  // Click the child-selector card to open dropdown
  const selector = page.locator('[data-testid="sidebar-child-selector"]');
  if (await selector.count() > 0) {
    await selector.click();
    await page.waitForTimeout(800);
  } else {
    console.log('Sidebar child selector not found');
  }

  await page.screenshot({ path: '/tmp/child-switcher-open.png', fullPage: false });
  console.log('Screenshot saved to /tmp/child-switcher-open.png');

  await browser.close();
})();
