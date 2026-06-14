const { firefox } = require('playwright');

(async () => {
  const browser = await firefox.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();

  // Intercept bundle requests to find the JS URL
  const scriptUrls = [];
  page.on('response', (response) => {
    const url = response.url();
    if (url.includes('bundle') || url.includes('.js?')) {
      scriptUrls.push(url);
    }
  });

  await page.goto('http://localhost:8081/login', { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);

  // Get the bundle script src from the page
  const scripts = await page.evaluate(() =>
    Array.from(document.querySelectorAll('script[src]')).map(s => s.src)
  );

  console.log('Script tags:', scripts.slice(0, 3));
  console.log('Bundle URLs intercepted:', scriptUrls.slice(0, 3));

  await browser.close();
})();
