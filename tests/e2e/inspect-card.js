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
  await page.waitForTimeout(3000);

  const info = await page.evaluate(() => {
    const kpiRegion = document.querySelector('[data-testid="overview-kpi-region"]');
    if (!kpiRegion) return { error: 'not found' };
    const card = kpiRegion.firstElementChild;
    return {
      className: card?.className,
      tagName: card?.tagName,
      inlineStyle: card?.getAttribute('style'),
      computedBorderRadius: window.getComputedStyle(card).borderRadius,
      // Check if any CSS var is used
      computedStyle_all: (() => {
        const cs = window.getComputedStyle(card);
        const relevant = {};
        for (const key of ['borderRadius','borderTopLeftRadius','borderTopRightRadius','borderBottomLeftRadius','borderBottomRightRadius','overflow','clipPath']) {
          relevant[key] = cs[key];
        }
        return relevant;
      })(),
    };
  });
  console.log(JSON.stringify(info, null, 2));
  await browser.close();
})();
