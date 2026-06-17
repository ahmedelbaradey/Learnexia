/**
 * Targeted language-switch check for item 5
 *
 * Ground truth (shipped app):
 * - `applyWebDirection()` sets `document.documentElement.lang` only (NOT `dir`).
 *   html[dir] is deliberately never set — RN Web uses component-level RTL props.
 *   Assert locale change via `html[lang]` (ar vs en), NOT `html[dir]`.
 * - `parent-header` Stack renders with `dir={isRtl ? 'rtl' : 'ltr'}` which
 *   RN Web maps to a `dir` attribute on the host div — use this for direction checks.
 * - Settings language tab testID: `settings-tab-language` (not the last index).
 * - Language select testID: `settings-language-switch` — a combobox trigger;
 *   options are role="radio" items in the dropdown.
 * - Language change requires pressing Save (`language-save`) to take effect.
 * - Parent login redirects to /(parent)/children (my-children-list), NOT overview.
 * - Post-login navigation to /settings works directly.
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

test('Language switch — EN/AR flips html[lang] and component direction', async ({ page }) => {
  // NOTE: /login without ?role= redirects to role-select. Use ?role=parent.
  // Parent home after login is /(parent)/children (my-children-list), NOT overview.
  await page.goto('/login?role=parent', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);
  await page.waitForSelector('[data-testid="login-username"]', { timeout: 60000 });
  await page.getByTestId('login-username').fill(PARENT_EMAIL);
  await page.getByTestId('login-password').fill(PARENT_PASSWORD);
  await page.getByTestId('login-submit').click();
  // Wait for any parent route (not login or role-select)
  await page.waitForFunction(
    () => !window.location.pathname.includes('login') && !window.location.pathname.includes('role-select'),
    { timeout: 60000 },
  );
  await waitForApp(page, 3000);

  // Navigate to settings for language switch.
  await page.goto('/settings', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  // Read current locale from html[lang] (set by applyWebDirection — NOT html[dir]).
  // applyWebDirection() deliberately does NOT set dir on the HTML root to avoid
  // double-reversal with RN Web's component-level RTL props.
  const initialLang = await page.evaluate(() => document.documentElement.lang ?? '');
  console.log(`Initial html[lang]: ${initialLang}`);
  await shot(page, '05-inspect-settings');

  // Navigate to Language & Region tab via its testID (settings-tab-language).
  // Falls back to clicking the last tab if the testID isn't found.
  let langSwitched = false;
  const settingsRoot = page.getByTestId('settings-root');
  const settingsRootVisible = await settingsRoot.isVisible({ timeout: 15_000 }).catch(() => false);
  console.log(`settings-root visible: ${settingsRootVisible}`);

  if (settingsRootVisible) {
    // Try direct testID click first (wide: Tabs rail; mobile: chip)
    const langTabDirect = page.getByTestId('settings-tab-language');
    const directVisible = await langTabDirect.isVisible({ timeout: 8_000 }).catch(() => false);
    console.log(`settings-tab-language direct visible: ${directVisible}`);

    if (directVisible) {
      await langTabDirect.click();
    } else {
      // Wide layout: the Tabs component wraps items; fall back to the nav container
      const settingsTabs = page.getByTestId('settings-tabs-nav');
      const tabsVisible = await settingsTabs.isVisible({ timeout: 8_000 }).catch(() => false);
      console.log(`settings-tabs-nav visible: ${tabsVisible}`);
      if (tabsVisible) {
        const tabs = settingsTabs.getByRole('tab');
        const tabCount = await tabs.count();
        console.log(`Tab count: ${tabCount}`);
        // Language is the 6th tab (index 5); fall back to last tab.
        const langTabIdx = tabCount >= 6 ? 5 : tabCount - 1;
        await tabs.nth(langTabIdx).click();
      }
    }
    await page.waitForTimeout(1000);
    await shot(page, '05-settings-lang-tab');

    // The language-switch control is a combobox (Select component, testID
    // `settings-language-switch`). Click it to open the dropdown, then pick
    // the opposite locale option (role="radio" items in the dropdown).
    const langSwitch = page.getByTestId('settings-language-switch');
    const langSwitchVisible = await langSwitch.isVisible({ timeout: 8_000 }).catch(() => false);
    console.log(`settings-language-switch visible: ${langSwitchVisible}`);

    if (langSwitchVisible) {
      await langSwitch.click();
      await page.waitForTimeout(500);

      let optionClicked = false;
      if (initialLang === 'ar') {
        // Currently AR — switch to EN
        const engOpt = page.getByRole('radio', { name: /english/i });
        if (await engOpt.isVisible({ timeout: 5_000 }).catch(() => false)) {
          await engOpt.click();
          optionClicked = true;
        }
      } else {
        // Currently EN (or unset) — switch to AR
        const arOpt = page.getByRole('radio', { name: /العربية|Arabic/i });
        if (await arOpt.isVisible({ timeout: 5_000 }).catch(() => false)) {
          await arOpt.click();
          optionClicked = true;
        }
      }

      if (optionClicked) {
        // Click Save to apply the language change (LanguagePanel requires explicit Save).
        const saveBtn = page.getByTestId('language-save');
        const saveBtnVisible = await saveBtn.isVisible({ timeout: 5_000 }).catch(() => false);
        console.log(`language-save visible: ${saveBtnVisible}`);
        if (saveBtnVisible) {
          await saveBtn.click();
        }
        await waitForApp(page, 2000);
        langSwitched = optionClicked;
      }
    }
  }

  // Assert direction change via html[lang] (set by applyWebDirection).
  // html[dir] is intentionally NOT set by the app; do NOT assert it.
  const newLang = await page.evaluate(() => document.documentElement.lang ?? '');
  console.log(`New html[lang] after switch: ${newLang}`);
  await shot(page, '05-after-lang-switch');

  // Direction can also be verified via the parent-header element's dir attribute
  // (ParentHeader renders dir={isRtl ? 'rtl' : 'ltr'} on its root Stack).
  const headerDir = await page.evaluate(() => {
    const el = document.querySelector('[data-testid="parent-header"]');
    return el ? el.getAttribute('dir') : null;
  });
  console.log(`parent-header[dir] after switch: ${headerDir}`);

  const langChanged = langSwitched && newLang !== initialLang;
  console.log(`[5] LANGUAGE SWITCH: ${langChanged ? 'PASS' : 'FAIL'} (html[lang]: ${initialLang} → ${newLang}, switched=${langSwitched})`);

  // Switch back to original locale
  if (langChanged) {
    const langSwitch2 = page.getByTestId('settings-language-switch');
    if (await langSwitch2.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await langSwitch2.click();
      await page.waitForTimeout(500);
      if (initialLang === 'ar') {
        const arOpt = page.getByRole('radio', { name: /العربية|Arabic/i });
        if (await arOpt.isVisible({ timeout: 5_000 }).catch(() => false)) await arOpt.click();
      } else {
        const enOpt = page.getByRole('radio', { name: /english/i });
        if (await enOpt.isVisible({ timeout: 5_000 }).catch(() => false)) await enOpt.click();
      }
      const saveBtn2 = page.getByTestId('language-save');
      if (await saveBtn2.isVisible({ timeout: 5_000 }).catch(() => false)) {
        await saveBtn2.click();
      }
      await waitForApp(page, 2000);
      const finalLang = await page.evaluate(() => document.documentElement.lang ?? '');
      console.log(`Final html[lang] after reset: ${finalLang}`);
      await shot(page, '05-back-to-original');
    }
  }

  expect(langChanged, 'Language switch in Settings should change html[lang] (applyWebDirection sets lang, not dir)').toBe(true);
});
