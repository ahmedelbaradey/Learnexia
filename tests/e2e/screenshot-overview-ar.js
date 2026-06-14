const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();

  // Set Arabic locale
  await page.goto('http://localhost:8081/login', { waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(2000);
  await page.evaluate(() => { localStorage.setItem('lx_locale', 'ar'); });
  await page.reload({ waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(3000);

  // Fill login form using testIDs
  await page.locator('[data-testid="login-username"]').fill('demo.parent@learnexia.com');
  await page.locator('[data-testid="login-password"]').fill('Demo!Pass1');
  await page.locator('[data-testid="login-submit"]').click();

  // Wait for navigation to overview
  await page.waitForURL('**/overview', { timeout: 15000 }).catch(() => {});
  await page.waitForTimeout(3000);

  console.log('URL:', page.url());
  const state = await page.evaluate(() => ({
    dir: document.documentElement.dir,
    lang: document.documentElement.lang,
    url: location.pathname,
  }));
  console.log('State:', JSON.stringify(state));

  await page.screenshot({ path: '/tmp/overview-ar.png', fullPage: false });
  console.log('Screenshot saved');

  await browser.close();
})();
