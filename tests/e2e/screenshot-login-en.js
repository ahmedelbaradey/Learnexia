const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();

  await page.goto('http://localhost:8081/login', { waitUntil: 'networkidle' });
  await page.reload({ waitUntil: 'networkidle' });

  // Switch to English locale
  await page.evaluate(() => {
    localStorage.setItem('lx_locale', 'en');
  });
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForTimeout(2500);

  const state = await page.evaluate(() => ({
    lang: document.documentElement.lang,
    lxLocale: localStorage.getItem('lx_locale'),
  }));
  console.log('Page state:', JSON.stringify(state));

  await page.screenshot({ path: '/tmp/login-desktop-en.png', fullPage: false });
  console.log('English desktop screenshot saved');

  await browser.close();
})();
