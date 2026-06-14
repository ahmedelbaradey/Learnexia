import { test, expect } from '@playwright/test';

test.setTimeout(120_000);

test('DIAG — check locale after login', async ({ page }) => {
  await page.goto('http://localhost:8081/login');
  await page.waitForTimeout(3000);
  
  // Check initial dir
  const dir0 = await page.evaluate(() => document.documentElement.dir);
  console.log('dir BEFORE locale switch:', dir0);
  
  // Try locale switch
  const arBtn = page.getByTestId('locale-switch-ar');
  const arBtnVisible = await arBtn.isVisible({ timeout: 5_000 }).catch(() => false);
  console.log('locale-switch-ar visible:', arBtnVisible);
  
  if (arBtnVisible) {
    await arBtn.click();
    await page.waitForTimeout(1000);
    const dir1 = await page.evaluate(() => document.documentElement.dir);
    console.log('dir AFTER locale-switch-ar click:', dir1);
  }
  
  // Login
  await page.getByTestId('login-username').fill('demo.parent@learnexia.com');
  await page.getByTestId('login-password').fill('Demo!Pass1');
  await page.getByTestId('login-submit').click();
  await page.waitForFunction(() => !window.location.pathname.includes('/login'), { timeout: 60_000 });
  await page.waitForTimeout(2000);
  
  const dir2 = await page.evaluate(() => document.documentElement.dir);
  const loc = await page.evaluate(() => {
    try { return localStorage.getItem('locale') || localStorage.getItem('i18nextLng') || 'none'; }
    catch(e) { return 'error'; }
  });
  console.log('dir AFTER login redirect:', dir2, 'localStorage locale:', loc);
  console.log('current URL:', page.url());
  
  // Screenshot
  await page.screenshot({ path: '/tmp/rtl-reverify/DIAG-after-login.png' });
  
  expect(dir2).toBe('rtl');
});
