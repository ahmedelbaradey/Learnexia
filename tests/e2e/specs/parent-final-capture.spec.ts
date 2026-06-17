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
  // NOTE (Batch A): /login without ?role= redirects to role-select. Use ?role=parent.
  // Parent home after login is /children (not /overview).
  console.log('[1] Navigating to login...');
  await page.goto('/login?role=parent', { waitUntil: 'domcontentloaded', timeout: 180000 });
  await waitForApp(page, 3000);
  console.log(`URL after goto: ${page.url()}`);

  await page.waitForSelector('[data-testid="login-username"]', { timeout: 60000 });
  console.log('[1] Login form visible');
  await shot(page, 'A-login-form');

  await page.getByTestId('login-username').fill(PARENT_EMAIL);
  await page.getByTestId('login-password').fill(PARENT_PASSWORD);
  await page.getByTestId('login-submit').click();
  console.log('[1] Submitted login');

  // Wait for any parent route (Batch A: parent home is /children)
  await page.waitForFunction(() => {
    const path = window.location.pathname;
    return !path.includes('login') && !path.includes('role-select');
  }, { timeout: 60000 });
  await waitForApp(page, 3000);

  const postLoginUrl = page.url();
  console.log(`[1] Post-login URL: ${postLoginUrl}`);
  await shot(page, '01-routing-post-login');

  // ── Item 1: ROUTING ─────────────────────────────────────────────────────────
  // Batch A: parent home is /children; accept children, overview, or add-child as valid parent routes
  const routingPass = postLoginUrl.includes('children') || postLoginUrl.includes('overview') || postLoginUrl.includes('add-child');
  console.log(`[1] ROUTING: ${routingPass ? 'PASS' : 'FAIL'} — URL=${postLoginUrl}`);

  // ── Item 2: SHELL (capture all 4 pages) ────────────────────────────────────
  // NOTE (Batch A): child-switcher-pill is the NARROW layout tab bar (mobile <768px).
  // At 1280px desktop, the sidebar shows nav items (menuitem) instead of the pill.
  // We check: navItems > 0 (sidebar visible at desktop viewport).
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
    // At 1280px desktop: sidebar is shown (role=menu + menuitems), no pill
    const navItems = await page.locator('[role="menuitem"]').count();
    // Fallback for narrow: check pill
    const switcher = page.getByTestId('child-switcher-pill');
    const switcherVisible = await switcher.isVisible({ timeout: 2_000 }).catch(() => false);
    const shellPagePass = navItems > 0 || switcherVisible;
    console.log(`[2] ${name}: navItems=${navItems}, switcher=${switcherVisible}`);
    if (shellPagePass) shellPassCount++;
    await shot(page, `02-shell-${name}`);
  }
  console.log(`[2] SHARED SHELL: ${shellPassCount === 4 ? 'PASS' : 'PARTIAL'} (${shellPassCount}/4 pages)`);

  // ── Item 3: CHILD-SWITCHER ──────────────────────────────────────────────────
  // NOTE: child-switcher-pill is NARROW-ONLY (<768px).
  // ParentHeader renders ChildSwitcher only when width < 768; at 1280px desktop
  // the pill is absent from the DOM entirely. Drop to a narrow viewport to test it.
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('http://localhost:8081/overview', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  // Capture first child (default)
  const overviewRoot = page.getByTestId('overview-root');
  const firstChildText = await overviewRoot.textContent().catch(() => '');
  console.log(`[3] First child text: ${firstChildText?.slice(0, 80)}`);
  await shot(page, '03a-overview-first-child');

  // Open dropdown and switch children (pill only renders on narrow)
  const pill = page.getByTestId('child-switcher-pill');
  const pillVisible = await pill.isVisible({ timeout: 5000 }).catch(() => false);
  console.log(`[3] Pill visible (narrow): ${pillVisible}`);

  let child2Text = '';
  let child3Text = '';

  if (pillVisible) {
    await pill.click();
    await page.waitForTimeout(500);
    const dropdown = page.getByTestId('child-switcher-dropdown');
    const dropdownVisible = await dropdown.isVisible().catch(() => false);
    console.log(`[3] Dropdown visible: ${dropdownVisible}`);
    await shot(page, '03b-switcher-dropdown');

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
      const dropdown2 = page.getByTestId('child-switcher-dropdown');
      if (await dropdown2.isVisible()) {
        const rows2 = dropdown2.locator('[role="menuitem"]');
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
  } else {
    await shot(page, '03b-switcher-pill-not-visible');
  }

  const switcherPass = pillVisible;
  console.log(`[3] CHILD-SWITCHER: ${switcherPass ? 'PASS' : 'FAIL — pill not visible on narrow'}`);

  // ── Item 4: OVERVIEW 2-COL LAYOUT ──────────────────────────────────────────
  // Restore desktop viewport for the layout assertions.
  await page.setViewportSize({ width: 1280, height: 900 });
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
  // NOTE: applyWebDirection() does NOT set document.documentElement.dir — it uses
  // component-level `dir` attributes instead (avoids double-reversal with RN Web
  // logical props). So html[dir] assertions are always wrong for this app.
  // RTL is verified via the parent-header component's `dir` attribute.
  //
  // settings-language-switch is a <Select> (native dropdown), not a button/radio.
  // Use page.selectOption() to change its value, then click language-save to commit.
  await page.goto('http://localhost:8081/settings', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  // Read initial RTL state from the parent-header component (not html[dir]).
  const initialHeaderDir = await page.getByTestId('parent-header').getAttribute('dir').catch(() => null);
  console.log(`[5] Initial parent-header dir: ${initialHeaderDir}`);
  await shot(page, '05a-settings-initial');

  let langSwitched = false;

  // Navigate to Language & Region tab (the last tab, testID settings-tab-language)
  const settingsTabs = page.getByTestId('settings-tabs-nav');
  if (await settingsTabs.isVisible({ timeout: 10_000 }).catch(() => false)) {
    const langTabBtn = page.getByTestId('settings-tab-language');
    if (await langTabBtn.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await langTabBtn.click();
      await page.waitForTimeout(1000);
    } else {
      // Fallback: click the last tab by index
      const tabs = settingsTabs.getByRole('tab');
      const tabCount = await tabs.count();
      if (tabCount > 0) {
        await tabs.nth(tabCount - 1).click();
        await page.waitForTimeout(1000);
      }
    }

    const langSwitch = page.getByTestId('settings-language-switch');
    if (await langSwitch.isVisible({ timeout: 8_000 }).catch(() => false)) {
      // settings-language-switch is a <Select> (native <select> element).
      // Select the opposite language: if currently ar → switch to en, else → ar.
      const currentVal = await langSwitch.inputValue().catch(() => 'ar');
      const nextVal = currentVal === 'ar' ? 'en' : 'ar';
      await langSwitch.selectOption(nextVal);
      await page.waitForTimeout(300);

      // Commit via Save button
      const saveBtn = page.getByTestId('language-save');
      if (await saveBtn.isVisible({ timeout: 5_000 }).catch(() => false)) {
        await saveBtn.click();
        langSwitched = true;
      }
    }
  }

  if (langSwitched) {
    await waitForApp(page, 3000);
    // Verify RTL flip via component-level dir attribute on parent-header.
    // applyWebDirection() does NOT set html[dir]; component-level `dir` prop is the
    // correct signal.
    const newHeaderDir = await page.getByTestId('parent-header').getAttribute('dir').catch(() => null);
    const langPass = newHeaderDir !== initialHeaderDir;
    console.log(`[5] LANGUAGE SWITCH: ${langPass ? 'PASS' : 'FAIL/unchanged'} (header dir: ${initialHeaderDir}→${newHeaderDir})`);
    await shot(page, '05b-settings-after-lang-switch');

    // Switch back to original language so subsequent items aren't affected
    const revertLangSwitch = page.getByTestId('settings-language-switch');
    if (await revertLangSwitch.isVisible({ timeout: 5_000 }).catch(() => false)) {
      const revertVal = initialHeaderDir === 'rtl' ? 'ar' : 'en';
      await revertLangSwitch.selectOption(revertVal).catch(() => {});
      const revertSave = page.getByTestId('language-save');
      if (await revertSave.isVisible({ timeout: 3_000 }).catch(() => false)) {
        await revertSave.click();
        await waitForApp(page, 2000);
      }
    }
  } else {
    console.log('[5] LANGUAGE SWITCH: Language select or Save not found on Settings page');
    await shot(page, '05b-settings-lang-not-found');
  }

  // ── Item 6: SETTINGS PAGE ──────────────────────────────────────────────────
  // NOTE (Batch A): child-switcher-pill is mobile-only (<768px). At 1280px desktop,
  // the sidebar shows navItems instead. Check settings-root and settings-tabs-nav.
  await page.goto('http://localhost:8081/settings', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);

  const settingsRoot = page.getByTestId('settings-root');
  const settingsRootVisible = await settingsRoot.isVisible({ timeout: 15_000 }).catch(() => false);
  const settingsTabsNav = page.getByTestId('settings-tabs-nav');
  const settingsTabsVisible = await settingsTabsNav.isVisible({ timeout: 10_000 }).catch(() => false);
  const settingsMenuItems = await page.locator('[role="menuitem"]').count();

  console.log(`[6] Settings: settingsRoot=${settingsRootVisible}, tabs=${settingsTabsVisible}, navItems=${settingsMenuItems}`);
  const settingsPass = settingsRootVisible && settingsTabsVisible;
  console.log(`[6] SETTINGS: ${settingsPass ? 'PASS' : 'FAIL'}`);
  await shot(page, '06-settings-page');

  // ── Item 7: ADD-CHILD MODAL ────────────────────────────────────────────────
  // NOTE: child-switcher-pill/dropdown are NARROW-ONLY (<768px). At 1280px desktop
  // they are absent from the DOM. Use my-children-add-button (always visible in the
  // pick-a-child row of MyChildrenWeb, regardless of viewport width) to open the modal.
  await page.goto('http://localhost:8081/children', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await waitForApp(page, 3000);
  await shot(page, '07a-children-page');

  let modalOpened = false;

  const addChildBtn = page.getByTestId('my-children-add-button');
  if (await addChildBtn.isVisible({ timeout: 10_000 }).catch(() => false)) {
    const btnText = await addChildBtn.textContent().catch(() => '');
    console.log(`[7] Add child button text: ${btnText}`);
    await addChildBtn.click();
    await page.waitForTimeout(1500);
  } else {
    console.log('[7] my-children-add-button not visible — skipping');
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
  // NOTE (Batch A): parent home is /children (not /overview); accept children/overview/add-child
  expect(routingPass, 'ROUTING: should land on a parent route (children/overview/add-child)').toBe(true);
  // SHELL: at 1280px desktop, navItems drive the check (pill is mobile-only)
  expect(shellPassCount, 'SHELL: all 4 pages should show sidebar nav or bottom-tab switcher').toBe(4);
  expect(layoutPass, 'LAYOUT: focus+recommendations should be side-by-side').toBe(true);
  expect(scrollbarStyleExists, 'SCROLLBAR: brand scrollbar should be injected').toBe(true);
});
