const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 900 },
  });
  const page = await context.newPage();

  // Force a hard reload to bypass any cached bundles
  await page.goto('http://localhost:8081/login', { waitUntil: 'networkidle' });
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);

  // Read state and also check actual rendered text
  const state = await page.evaluate(() => ({
    dir: document.documentElement.dir,
    lang: document.documentElement.lang,
    lxLocale: localStorage.getItem('lx_locale'),
    titleText: document.body.innerText.substring(0, 300),
  }));
  console.log('Page state:', JSON.stringify(state, null, 2));

  // Desktop screenshot (brand panel + form)
  await page.screenshot({ path: '/tmp/login-desktop-ar.png', fullPage: false });
  console.log('Desktop screenshot saved');

  // Mobile screenshot
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(800);
  await page.screenshot({ path: '/tmp/login-mobile-ar.png', fullPage: false });
  console.log('Mobile screenshot saved');

  await browser.close();
  console.log('Done');
})();
