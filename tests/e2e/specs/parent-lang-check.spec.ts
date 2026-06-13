/**
 * Targeted language-switch check for item 5
 */
import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const PARENT_EMAIL = 'demo.parent@learnexia.com';
const PARENT_PASSWORD = 'Demo!Pass1';
const SHOTS_DIR = '/tmp/parent-shots';

if (!fs.existsSync(SHOTS_DIR)) {
  fs.mkdirSync(SHOTS_DIR, { recursive: true });
}

async function shot(page: Page, name: string): Promise<string> {
  const filePath = path.join(SHOTS_DIR, `${name}.png`);
  await page.screenshot({ path: filePath, fullPage: false });
  console.log(`[SHOT] ${filePath}`);
  return filePath;
}

async function waitForApp(page: Page, extraMs = 3000): Promise<void> {
  try {
    await page.waitForLoadState('networkidle', { timeout: 30000 });
  } catch { /* Metro websocket keeps it open */ }
  await page.waitForTimeout(extraMs);
}

test.use({ viewport: { width: 1280, height: 900 } });

test('Language switch — EN/AR flips dir attribute', async ({ page }) => {
  // Login
  await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 180000 });
  await waitForApp(page, 5000);
  await page.waitForSelector('[data-testid="login-username"]', { timeout: 120000 });
  await page.getByTestId('login-username').fill(PARENT_EMAIL);
  await page.getByTestId('login-password').fill(PARENT_PASSWORD);
  await page.getByTestId('login-submit').click();
  await page.waitForFunction(() => window.location.pathname.includes('overview'), { timeout: 60000 });
  await waitForApp(page, 3000);

  // Capture overview and inspect DOM for language buttons
  const initialDir = await page.evaluate(() => document.documentElement.getAttribute('dir') ?? 'ltr');
  console.log(`Initial dir: ${initialDir}`);

  // Log all clickable elements in the header area (top 60px)
  const headerElements = await page.evaluate(() => {
    const elems = document.querySelectorAll('*');
    const results: string[] = [];
    elems.forEach(el => {
      const rect = el.getBoundingClientRect();
      if (rect.top < 60 && rect.top >= 0 && rect.width > 10 && rect.height > 5) {
        const tag = el.tagName;
        const role = el.getAttribute('role');
        const ariaLabel = el.getAttribute('aria-label');
        const testId = el.getAttribute('data-testid');
        const text = (el as HTMLElement).innerText?.slice(0, 30);
        if (role || testId || text) {
          results.push(`${tag}[role=${role}][data-testid=${testId}][aria-label=${ariaLabel}]: "${text}"`);
        }
      }
    });
    return results.slice(0, 30);
  });
  console.log('Header elements:', JSON.stringify(headerElements, null, 2));

  await shot(page, '05-inspect-header');

  // Try different selectors for the language button
  const selectors = [
    'text=English',
    '[aria-label="English"]',
    '[aria-label="Switch to English"]',
    '[data-testid="lang-en"]',
    '[data-testid="locale-en"]',
    'button:has-text("English")',
  ];

  let switched = false;
  for (const sel of selectors) {
    const count = await page.locator(sel).count();
    console.log(`Selector "${sel}": count=${count}`);
    if (count > 0) {
      const firstText = await page.locator(sel).first().textContent().catch(() => '');
      console.log(`  text="${firstText}"`);
    }
  }

  // Also try: find any element with "English" text anywhere in header
  const englishEls = await page.evaluate(() => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    const results: string[] = [];
    let node;
    while ((node = walker.nextNode())) {
      if (node.textContent?.includes('English')) {
        const parent = node.parentElement;
        if (parent) {
          const rect = parent.getBoundingClientRect();
          results.push(`${parent.tagName}[role=${parent.getAttribute('role')}][testid=${parent.getAttribute('data-testid')}] at (${Math.round(rect.x)},${Math.round(rect.y)}) text="${node.textContent}"`);
        }
      }
    }
    return results;
  });
  console.log('Elements with "English" text:', JSON.stringify(englishEls, null, 2));

  // Click "English" button by text content direct evaluation
  const clicked = await page.evaluate(() => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let node;
    while ((node = walker.nextNode())) {
      if (node.textContent?.trim() === 'English') {
        const parent = node.parentElement;
        if (parent) {
          (parent as HTMLElement).click();
          return `clicked: ${parent.tagName}[role=${parent.getAttribute('role')}]`;
        }
      }
    }
    return 'not found';
  });
  console.log(`Click result: ${clicked}`);

  await waitForApp(page, 3000);
  const newDir = await page.evaluate(() => document.documentElement.getAttribute('dir') ?? 'ltr');
  console.log(`New dir after click: ${newDir}`);
  await shot(page, '05-after-en-click');

  const langPass = newDir !== initialDir;
  console.log(`[5] LANGUAGE SWITCH: ${langPass ? 'PASS' : 'FAIL'} (${initialDir} → ${newDir})`);

  // Switch back to AR
  if (langPass) {
    const arClicked = await page.evaluate(() => {
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      let node;
      while ((node = walker.nextNode())) {
        if (node.textContent?.trim() === 'العربية' || node.textContent?.trim() === 'عربي') {
          const parent = node.parentElement;
          if (parent) {
            (parent as HTMLElement).click();
            return `clicked: ${parent.tagName}[role=${parent.getAttribute('role')}]`;
          }
        }
      }
      return 'not found';
    });
    console.log(`AR click result: ${arClicked}`);
    await waitForApp(page, 3000);
    const finalDir = await page.evaluate(() => document.documentElement.getAttribute('dir') ?? 'ltr');
    console.log(`Final dir after AR: ${finalDir}`);
    await shot(page, '05-back-to-ar');
  }

  expect(langPass, 'Language switch should flip dir attribute').toBe(true);
});
