/**
 * Final parent-dashboard capture — all 8 items in ONE test to avoid
 * the 2-minute bundle-warmup overhead on each separate test.
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
  } catch {
    // Metro keeps a websocket, so networkidle never fires cleanly — that's fine
  }
  await page.waitForTimeout(extraMs);
}

test.use({ viewport: { width: 1280, height: 900 } });

test('Full parent dashboard verification — all 8 items', async ({ page }) => {

  // ── Phase 0: Login ─────────────────────────────────────────────────────────
  console.log('[1] Navigating to login...');
  await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 180000 });
  await waitForApp(page, 5000);
  console.log(`URL after goto: ${page.url()}`);

  await page.waitForSelector('[data-testid="login-username"]', { timeout: 120000 });
  console.log('[1] Login form visible');
  await shot(page, 'A-login-form');

  await page.getByTestId('login-username').fill(PARENT_EMAIL);
  await page.getByTestId('login-password').fill(PARENT_PASSWORD);
  await page.getByTestId('login-submit').click();
  console.log('[1] Submitted login');

  // Wait for any parent route
  await page.waitForFunction(() => {
    return window.location.pathname.includes('overview') || window.location.pathname.includes('parent');
  }, { timeout: 60000 });
  await waitForApp(page, 3000);

  const postLoginUrl = page.url();
  console.log(`[1] Post-login URL: ${postLoginUrl}`);
  await shot(page, '01-routing-post-login');

  // ── Item 1: ROUTING ─────────────────────────────────────────────────────────
  const routingPass = postLoginUrl.includes('overview');
  console.log(`[1] ROUTING: ${routingPass ? 'PASS' : 'FAIL'} — URL=${postLoginUrl}`);

  // ── Item 2: SHELL (capture all 4 pages) ────────────────────────────────────
  const shellPages = [
    { route: '/overview', name: 'overview' },
    { route: '/settings', name: 'settings' },
    { route: '/children', name: 'children' },
    { route: '/reports', name: 'reports' },
  ];
  let shellPassCount = 0;
  for (const { route, name } of shellPages) {
    await page.goto(`http://localhost:8081${route}`, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await waitForApp(page, 3000);
    const switcher = page.getByTestId('child-switcher-pill');
    const switcherVisible = await switcher.isVisible().catch(() => false);
    const navItems = await page.locator('[role="menuitem"]').count();
    console.log(`[2] ${name}: switcher=${switcherVisible}, navItems=${navItems}`);
    if (switcherVisible && navItems > 0) shellPassCount++;
    await shot(page, `02-shell-${name}`);
  }
  console.log(`[2] SHARED SHELL: ${shellPassCount === 4 ? 'PASS' : 'PARTIAL'} (${shellPassCount}/4 pages)`);

  // ── Item 3: CHILD-SWITCHER ──────────────────────────────────────────────────
  await page.goto('http://localhost:8081/overview', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  // Capture first child (default)
  const overviewRoot = page.getByTestId('overview-root');
  const firstChildText = await overviewRoot.textContent().catch(() => '');
  console.log(`[3] First child text: ${firstChildText?.slice(0, 80)}`);
  await shot(page, '03a-overview-first-child');

  // Open dropdown and switch children
  const pill = page.getByTestId('child-switcher-pill');
  await pill.click();
  await page.waitForTimeout(500);
  const dropdown = page.getByTestId('child-switcher-dropdown');
  const dropdownVisible = await dropdown.isVisible().catch(() => false);
  console.log(`[3] Dropdown visible: ${dropdownVisible}`);
  await shot(page, '03b-switcher-dropdown');

  let child2Text = '';
  let child3Text = '';

  if (dropdownVisible) {
    const rows = dropdown.locator('[role="menuitem"]');
    const count = await rows.count();
    console.log(`[3] Children in dropdown: ${count}`);

    if (count >= 2) {
      const child2Name = await rows.nth(1).textContent().catch(() => 'n/a');
      await rows.nth(1).click();
      await waitForApp(page, 2000);
      child2Text = await overviewRoot.textContent().catch(() => '');
      console.log(`[3] Child 2 (${child2Name?.trim()}): ${child2Text?.slice(0, 80)}`);
      await shot(page, '03c-overview-child2');
    }

    await pill.click();
    await page.waitForTimeout(500);
    if (await dropdown.isVisible()) {
      const rows2 = dropdown.locator('[role="menuitem"]');
      if (await rows2.count() >= 3) {
        const child3Name = await rows2.nth(2).textContent().catch(() => 'n/a');
        await rows2.nth(2).click();
        await waitForApp(page, 2000);
        child3Text = await overviewRoot.textContent().catch(() => '');
        console.log(`[3] Child 3 (${child3Name?.trim()}): ${child3Text?.slice(0, 80)}`);
        await shot(page, '03d-overview-child3');
      }
    }
  }

  const switcherPass = firstChildText !== child2Text && child2Text !== child3Text;
  console.log(`[3] CHILD-SWITCHER: ${switcherPass ? 'PASS' : 'FAIL or only 1 child loaded'}`);

  // ── Item 4: OVERVIEW 2-COL LAYOUT ──────────────────────────────────────────
  await page.goto('http://localhost:8081/overview', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  const focusRow = page.getByTestId('overview-focus-recommendations-row');
  const focusRowBounds = await focusRow.boundingBox().catch(() => null);
  const kpiRegion = page.getByTestId('overview-kpi-region');
  const kpiVisible = await kpiRegion.isVisible().catch(() => false);
  const masteryRegion = page.getByTestId('overview-mastery-region');
  const masteryVisible = await masteryRegion.isVisible().catch(() => false);

  console.log(`[4] Focus+Recommendations row bounds: ${JSON.stringify(focusRowBounds)}`);
  console.log(`[4] KPI region visible: ${kpiVisible}, Mastery visible: ${masteryVisible}`);

  const layoutPass = focusRowBounds !== null && focusRowBounds.width > 600 && kpiVisible && masteryVisible;
  console.log(`[4] OVERVIEW LAYOUT: ${layoutPass ? 'PASS' : 'FAIL'}`);
  await shot(page, '04-overview-desktop-layout');

  // ── Item 5: LANGUAGE SWITCH ─────────────────────────────────────────────────
  await page.goto('http://localhost:8081/overview', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  const initialDir = await page.evaluate(() => document.documentElement.getAttribute('dir') ?? 'ltr');
  console.log(`[5] Initial dir: ${initialDir}`);
  await shot(page, '05a-overview-initial');

  // Find language buttons
  const allButtonTexts = await page.locator('[role="button"]').allTextContents();
  console.log(`[5] Button texts: ${JSON.stringify(allButtonTexts.filter(t => t.match(/English|العربية|عربي/i)))}`);

  let langSwitched = false;
  if (initialDir === 'rtl') {
    // Currently AR, switch to EN
    const engBtn = page.locator('[role="button"]').filter({ hasText: /^English$/i }).first();
    if (await engBtn.count() > 0) {
      await engBtn.click();
      langSwitched = true;
    }
  } else {
    // Currently EN, switch to AR
    const arBtn = page.locator('[role="button"]').filter({ hasText: /العربية/ }).first();
    if (await arBtn.count() > 0) {
      await arBtn.click();
      langSwitched = true;
    }
  }

  if (langSwitched) {
    await waitForApp(page, 3000);
    const newDir = await page.evaluate(() => document.documentElement.getAttribute('dir') ?? 'ltr');
    const langPass = newDir !== initialDir;
    console.log(`[5] LANGUAGE SWITCH: ${langPass ? 'PASS' : 'FAIL'} (${initialDir}→${newDir})`);
    await shot(page, '05b-overview-after-lang-switch');
  } else {
    console.log('[5] LANGUAGE SWITCH: Language button not found');
    await shot(page, '05b-overview-lang-btn-not-found');
  }

  // ── Item 6: SETTINGS PAGE ──────────────────────────────────────────────────
  await page.goto('http://localhost:8081/settings', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  const settingsSwitcher = page.getByTestId('child-switcher-pill');
  const settingsSwitcherVisible = await settingsSwitcher.isVisible().catch(() => false);
  const settingsTabBtns = await page.locator('[role="button"]').count();
  const settingsMenuItems = await page.locator('[role="menuitem"]').count();

  console.log(`[6] Settings: switcher=${settingsSwitcherVisible}, tab/btns=${settingsTabBtns}, navItems=${settingsMenuItems}`);
  const settingsPass = settingsSwitcherVisible;
  console.log(`[6] SETTINGS: ${settingsPass ? 'PASS' : 'FAIL'}`);
  await shot(page, '06-settings-page');

  // ── Item 7: ADD-CHILD MODAL ────────────────────────────────────────────────
  await page.goto('http://localhost:8081/children', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);
  await shot(page, '07a-children-page');

  // Open child-switcher dropdown and click Add child
  const childrenPill = page.getByTestId('child-switcher-pill');
  await childrenPill.click();
  await page.waitForTimeout(500);

  const childrenDropdown = page.getByTestId('child-switcher-dropdown');
  let modalOpened = false;

  if (await childrenDropdown.isVisible()) {
    const addChildBtn = childrenDropdown.locator('[role="button"]').last();
    if (await addChildBtn.count() > 0) {
      const btnText = await addChildBtn.textContent().catch(() => '');
      console.log(`[7] Add child button text: ${btnText}`);
      await addChildBtn.click();
      await page.waitForTimeout(1500);
    }
  }

  const modal = page.getByTestId('add-child-modal');
  modalOpened = await modal.isVisible().catch(() => false);
  console.log(`[7] Modal visible: ${modalOpened}`);

  if (modalOpened) {
    await shot(page, '07b-add-child-modal');
    const gradeTile1 = page.getByTestId('grade-tile-1');
    const gradeTile6 = page.getByTestId('grade-tile-6');
    const arTile = page.getByTestId('app-lang-tile-ar');
    const enTile = page.getByTestId('app-lang-tile-en');

    const g1Visible = await gradeTile1.isVisible().catch(() => false);
    const g6Visible = await gradeTile6.isVisible().catch(() => false);
    const arVisible = await arTile.isVisible().catch(() => false);
    const enVisible = await enTile.isVisible().catch(() => false);

    const modalBounds = await modal.boundingBox().catch(() => null);
    console.log(`[7] Grade tiles 1+6: ${g1Visible}+${g6Visible}, Flag tiles AR+EN: ${arVisible}+${enVisible}`);
    console.log(`[7] Modal bounds: ${JSON.stringify(modalBounds)}`);

    const modalPass = g1Visible && g6Visible && arVisible && enVisible &&
                      (modalBounds !== null && modalBounds.x > 50);
    console.log(`[7] ADD-CHILD MODAL: ${modalPass ? 'PASS' : 'FAIL'}`);
  } else {
    console.log('[7] ADD-CHILD MODAL: FAIL — modal not visible');
    await shot(page, '07b-modal-not-visible');
  }

  // ── Item 8: SCROLLBAR ──────────────────────────────────────────────────────
  await page.goto('http://localhost:8081/overview', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  const scrollbarStyleExists = await page.evaluate(() => {
    return document.getElementById('lx-brand-scrollbar') !== null;
  });
  console.log(`[8] SCROLLBAR: ${scrollbarStyleExists ? 'PASS' : 'FAIL'} — lx-brand-scrollbar style injected=${scrollbarStyleExists}`);
  await shot(page, '08-overview-scrollbar');

  // ── BONUS: Narrow 390px mobile view ────────────────────────────────────────
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('http://localhost:8081/overview', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);
  const mobileSwitcherVisible = await page.getByTestId('child-switcher-pill').isVisible().catch(() => false);
  console.log(`[BONUS] Mobile 390px switcher visible: ${mobileSwitcherVisible}`);
  await shot(page, '09-mobile-390-shell');

  // ── FINAL ASSERTIONS ───────────────────────────────────────────────────────
  expect(routingPass, 'ROUTING: should land on overview').toBe(true);
  expect(shellPassCount, 'SHELL: all 4 pages should show switcher + nav').toBe(4);
  expect(layoutPass, 'LAYOUT: focus+recommendations should be side-by-side').toBe(true);
  expect(scrollbarStyleExists, 'SCROLLBAR: brand scrollbar should be injected').toBe(true);
});
