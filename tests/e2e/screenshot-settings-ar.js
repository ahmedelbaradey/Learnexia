const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();

  await page.goto('http://localhost:8081/login', { waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(1500);
  await page.evaluate(() => { localStorage.setItem('lx_locale', 'ar'); });
  await page.reload({ waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(2000);
  await page.locator('[data-testid="login-username"]').fill('demo.parent@learnexia.com');
  await page.locator('[data-testid="login-password"]').fill('Demo!Pass1');
  await page.locator('[data-testid="login-submit"]').click();
  await page.waitForURL('**/overview', { timeout: 15000 }).catch(() => {});
  await page.waitForTimeout(2000);

  // Navigate to settings
  await page.goto('http://localhost:8081/settings', { waitUntil: 'load', timeout: 30000 });
  await page.waitForTimeout(2500);

  console.log('URL:', page.url());
  await page.screenshot({ path: '/tmp/settings-ar.png', fullPage: false });
  console.log('Screenshot saved to /tmp/settings-ar.png');

  await browser.close();
})();
