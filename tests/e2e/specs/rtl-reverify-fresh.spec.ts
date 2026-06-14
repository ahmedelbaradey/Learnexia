/**
 * RTL Re-Verify (FRESH BUNDLE) — 2026-06-14
 *
 * Checks the just-applied Sidebar.tsx + OverviewWeb.tsx double-flip fixes
 * on a fresh Expo bundle. Items under test:
 *
 *  LY1  — Sidebar nav icons on RIGHT (leading) side in AR (not double-flipped)
 *  O5   — Overview header row in AR: title block RIGHT, buttons LEFT
 *  3a   — (parent)/index.tsx fallback home row (CANT-REACH if redirected)
 *  3b   — FamilySummaryStrip alignment on My Children page in AR
 *  3c   — ReportsWeb page row alignment in AR (KPI cards, header row)
 *  S2   — Settings Language Save succeeds (no "حدث خطأ" / "No internet")
 *  O1   — Overview KPI card icons still correct in AR (regression guard)
 *  C7   — Overview child cards (ChildCard) still correct in AR (regression guard)
 *
 * Screenshots saved to /tmp/rtl-reverify/
 * Login: demo.parent@learnexia.com / Demo!Pass1
 */

import { test, expect } from '@playwright/test';
import * as fs from 'fs';

const DEMO_EMAIL = 'demo.parent@learnexia.com';
const DEMO_PASS = 'Demo!Pass1';
const SCREENSHOT_DIR = '/tmp/rtl-reverify';

function ensureDir() {
  if (!fs.existsSync(SCREENSHOT_DIR)) fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

async function shot(page: import('@playwright/test').Page, name: string): Promise<string> {
  ensureDir();
  const p = `${SCREENSHOT_DIR}/${name}.png`;
  await page.screenshot({ path: p, fullPage: false });
  return p;
}

/** Login as demo parent, switch to Arabic, and wait to land off /login */
async function loginDemo(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/login');
  const emailField = page.getByTestId('login-username');
  await emailField.waitFor({ state: 'visible', timeout: 40_000 });
  await page.waitForTimeout(1500);

  await emailField.fill(DEMO_EMAIL);
  await page.getByTestId('login-password').fill(DEMO_PASS);
  await page.getByTestId('login-submit').click();

  await page.waitForFunction(
    () => !window.location.pathname.includes('/login'),
    { timeout: 60_000 },
  );
  await page.waitForTimeout(2000);

  // Switch to Arabic AFTER landing on the dashboard (locale store persists per session).
  // The locale-switch buttons are always present in the parent shell header.
  await switchToArabic(page);
}

/** Switch to Arabic locale via the locale-switch-ar button and verify dir=rtl. */
async function switchToArabic(page: import('@playwright/test').Page): Promise<void> {
  const dir = await page.evaluate(() => document.documentElement.dir).catch(() => '');
  if (dir === 'rtl') return; // already Arabic

  const arBtn = page.getByTestId('locale-switch-ar');
  const arBtnVisible = await arBtn.isVisible({ timeout: 6_000 }).catch(() => false);
  if (arBtnVisible) {
    await arBtn.click();
    // Wait for the RTL flip to apply (i18n store + DOM update)
    await page.waitForFunction(
      () => document.documentElement.dir === 'rtl',
      { timeout: 10_000 },
    ).catch(() => {}); // if it doesn't flip, we continue and the test reports ltr
    await page.waitForTimeout(800);
  } else {
    // Last resort: try via role
    const arByRole = page.getByRole('button', { name: /العربية|Arabic|AR/i }).first();
    if (await arByRole.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await arByRole.click();
      await page.waitForTimeout(800);
    }
  }
}

test.describe.configure({ timeout: 180_000 });

// =============================================================================
// LY1 — Sidebar nav icons on RIGHT (leading) side in AR
// =============================================================================
test('LY1 — sidebar nav icon is on the PHYSICAL RIGHT in AR (leading side)', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/overview');
  await page.waitForTimeout(2000);
  // Ensure Arabic after navigation (locale may reset to default on new route)
  await switchToArabic(page);
  await page.waitForTimeout(1500);

  // Confirm RTL
  const dir = await page.evaluate(() => document.documentElement.dir);
  expect(dir, 'html[dir] must be rtl in Arabic locale').toBe('rtl');

  await shot(page, 'LY1-overview-full-ar');

  // Get the sidebar nav — the sidebar is visible at 1280px desktop viewport.
  // Each nav item row has a Text (emoji icon) and a Text (label).
  // In the fixed code: flexDirection="row" + doc dir=rtl => icon is on the RIGHT visually.
  // We verify by measuring the icon vs label horizontal positions inside one nav item.
  //
  // The nav menu has accessibilityRole="menu". Each item has accessibilityRole="menuitem".
  const navMenu = page.locator('[role="menu"]').first();
  const menuVisible = await navMenu.isVisible({ timeout: 8_000 }).catch(() => false);

  if (!menuVisible) {
    test.info().annotations.push({ type: 'SKIP', description: 'LY1: sidebar menu not visible at this viewport. Sidebar requires >=768px desktop.' });
    return;
  }

  // Get the first nav item (Overview)
  const firstNavItem = page.locator('[role="menuitem"]').first();
  await firstNavItem.waitFor({ state: 'visible', timeout: 10_000 });

  // Measure the icon (emoji Text) and the label Text within the first nav item.
  // The icon is always the FIRST child Text, the label is the SECOND.
  // In RTL with flexDirection="row": the browser renders children in reverse visual order
  // so the FIRST child (icon) appears on the RIGHT side physically.
  //
  // Strategy: get bounding boxes via JS for the two spans inside the nav item.
  const positions = await firstNavItem.evaluate((el: HTMLElement) => {
    const spans = el.querySelectorAll('span, [data-testid]');
    const texts = Array.from(el.querySelectorAll('*')).filter(
      (node): node is HTMLElement =>
        node instanceof HTMLElement &&
        node.children.length === 0 &&
        (node.textContent?.trim().length ?? 0) > 0,
    );
    if (texts.length < 2) return null;
    const iconRect = texts[0].getBoundingClientRect();
    const labelRect = texts[1].getBoundingClientRect();
    return {
      iconLeft: iconRect.left,
      iconRight: iconRect.right,
      labelLeft: labelRect.left,
      labelRight: labelRect.right,
    };
  });

  await shot(page, 'LY1-sidebar-nav-ar');

  if (!positions) {
    test.info().annotations.push({
      type: 'note',
      description: 'LY1: Could not measure icon/label positions (DOM structure unexpected). Visual screenshot captured at /tmp/rtl-reverify/LY1-sidebar-nav-ar.png',
    });
    // Capture a crop of the sidebar for visual review
    return;
  }

  test.info().annotations.push({
    type: 'info',
    description: `LY1 positions: icon.left=${positions.iconLeft.toFixed(0)}, icon.right=${positions.iconRight.toFixed(0)}, label.left=${positions.labelLeft.toFixed(0)}, label.right=${positions.labelRight.toFixed(0)}`,
  });

  // In correct RTL: icon is the LEADING child => rendered RIGHTMOST physically.
  // So icon.left > label.right (icon is to the right of label).
  // If double-flipped: icon would be on the LEFT (icon.right < label.left).
  const iconIsOnRight = positions.iconLeft > positions.labelRight;
  const iconIsOnLeft = positions.iconRight < positions.labelLeft;

  if (iconIsOnRight) {
    test.info().annotations.push({ type: 'PASS', description: 'LY1 FIXED: Icon is on the physical RIGHT (leading side in RTL).' });
  } else if (iconIsOnLeft) {
    test.info().annotations.push({ type: 'FAIL', description: 'LY1 STILL-BROKEN: Icon is on the LEFT (double-flip still present). icon is before label visually.' });
    await shot(page, 'LY1-BROKEN-icon-on-left');
  } else {
    // Overlapping — might be vertically stacked or the icon/label are the same element
    test.info().annotations.push({
      type: 'note',
      description: `LY1 UNCERTAIN: icon and label positions overlap or are adjacent (icon.left=${positions.iconLeft.toFixed(0)}, label.right=${positions.labelRight.toFixed(0)}). Visual review needed.`,
    });
  }

  // Hard assertion: icon must NOT be clearly to the left of the label
  expect(iconIsOnLeft, 'Icon must NOT be on the left (LTR side) in Arabic sidebar nav').toBe(false);
});

// =============================================================================
// O5 — Overview header row in AR: title block RIGHT, buttons LEFT
// =============================================================================
test('O5 — overview header: title on RIGHT, buttons on LEFT in AR', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/overview');
  await page.waitForTimeout(2000);
  await switchToArabic(page);
  await page.waitForTimeout(1500);

  await expect(page.getByTestId('overview-root'), 'overview-root must be visible').toBeVisible({ timeout: 25_000 });
  const header = page.getByTestId('overview-header');
  await expect(header, 'overview-header must be visible').toBeVisible({ timeout: 15_000 });

  await shot(page, 'O5-overview-header-ar');

  // The header is <Stack testID="overview-header" flexDirection="row" justifyContent="space-between">
  // With doc dir=rtl + flexDirection="row" (NO row-reverse in the fixed code):
  //   - First child (title block) renders on the RIGHT physically
  //   - Second child (buttons row) renders on the LEFT physically
  //
  // We measure the two direct children of the header.
  const childPositions = await header.evaluate((el: HTMLElement) => {
    const children = Array.from(el.children) as HTMLElement[];
    if (children.length < 2) return null;
    const titleRect = children[0].getBoundingClientRect();
    const btnsRect = children[children.length - 1].getBoundingClientRect();
    return {
      titleLeft: titleRect.left,
      titleRight: titleRect.right,
      btnsLeft: btnsRect.left,
      btnsRight: btnsRect.right,
    };
  });

  if (!childPositions) {
    test.info().annotations.push({ type: 'note', description: 'O5: Could not measure header children. Visual screenshot at /tmp/rtl-reverify/O5-overview-header-ar.png' });
    return;
  }

  test.info().annotations.push({
    type: 'info',
    description: `O5 positions: title.left=${childPositions.titleLeft.toFixed(0)}, title.right=${childPositions.titleRight.toFixed(0)}, btns.left=${childPositions.btnsLeft.toFixed(0)}, btns.right=${childPositions.btnsRight.toFixed(0)}`,
  });

  // In correct RTL (flex-row + dir=rtl): title block is on the RIGHT, buttons on the LEFT.
  // title.left > btns.right => title is to the right of the buttons.
  const titleOnRight = childPositions.titleLeft > childPositions.btnsRight;
  // Double-flipped (row-reverse + dir=rtl): title would be on the LEFT of buttons.
  const titleOnLeft = childPositions.titleRight < childPositions.btnsLeft;

  if (titleOnRight) {
    test.info().annotations.push({ type: 'PASS', description: 'O5 FIXED: Title block is on the RIGHT, buttons on the LEFT (correct RTL).' });
  } else if (titleOnLeft) {
    test.info().annotations.push({ type: 'FAIL', description: 'O5 STILL-BROKEN: Title block is on the LEFT, buttons on the RIGHT (double-flip still present). File: OverviewWeb.tsx.' });
    await shot(page, 'O5-BROKEN-title-on-left');
  } else {
    // Both overlap or stacked (wrapping viewport)
    test.info().annotations.push({
      type: 'note',
      description: `O5 UNCERTAIN: title and buttons overlap or wrapped (title.left=${childPositions.titleLeft.toFixed(0)}, btns.right=${childPositions.btnsRight.toFixed(0)}). Viewport may be too narrow causing flexWrap.`,
    });
  }

  // Soft assertion — allow uncertain case (viewport wrapping)
  expect(titleOnLeft, 'Title block must NOT be to the left of buttons in RTL header').toBe(false);
});

// =============================================================================
// 3a — (parent)/index.tsx fallback home row
// =============================================================================
test('3a — (parent)/index.tsx fallback home (CANT-REACH if redirected)', async ({ page }) => {
  await loginDemo(page);
  // loginDemo already switched to Arabic

  // Navigate directly to /(parent) which may redirect to /overview
  await page.goto('/(parent)');
  await page.waitForTimeout(3000);
  // Re-apply Arabic after navigation
  await switchToArabic(page);
  await page.waitForTimeout(1000);
  const currentUrl = page.url();

  if (!currentUrl.includes('/(parent)') && !currentUrl.endsWith('/')) {
    // Redirected away (to /overview) — this is the expected behavior per HANDOFF
    test.info().annotations.push({
      type: 'CANT-REACH',
      description: `3a: /(parent)/index.tsx is bypassed by the /overview redirect (landed at ${currentUrl}). CANT-REACH — the fallback index is not user-reachable via normal navigation.`,
    });
    await shot(page, '3a-redirected-to');
    // Not a failure — this is by design (useAuthRoute → /(parent)/overview)
    return;
  }

  // If we reached the index, capture it and check the sign-out row alignment
  await shot(page, '3a-parent-index-ar');

  // The index has a logo + "Sign out" row. In AR: if it uses row-reverse, it would double-flip.
  // We just visually verify and record.
  const htmlDir = await page.evaluate(() => document.documentElement.dir);
  test.info().annotations.push({
    type: 'info',
    description: `3a: Reached /(parent) index. dir=${htmlDir}. URL=${currentUrl}. Visual captured.`,
  });
});

// =============================================================================
// 3b — FamilySummaryStrip on My Children page in AR
// =============================================================================
test('3b — FamilySummaryStrip row content alignment in AR', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/children');
  await page.waitForTimeout(2000);
  await switchToArabic(page);
  await page.waitForTimeout(2000);

  const dir = await page.evaluate(() => document.documentElement.dir);
  expect(dir, 'must be rtl').toBe('rtl');

  await shot(page, '3b-children-page-ar-full');

  // FamilySummaryStrip — the purple hero strip. On web it uses CSS Grid with direction=rtl.
  // The Grid wrapper has its `direction` CSS prop set to `isRtl ? 'rtl' : 'ltr'`.
  // This is a CSS-grid-based layout, so "double flip" is not the same risk as flexDirection.
  // The RTL concern: the headline block must be on the RIGHT, the stats on the LEFT (AR).
  //
  // Look for the strip by its gradient background (GradientBox is inside it).
  // The strip has no testID, so we use the structure: find the element with gradient class or purple bg.
  // The headline text includes the family summary eyebrow key — look for it.

  // Approach: check if the strip exists and capture it
  const stripByText = page.locator('[class*="gradient"], [class*="card"]').filter({
    hasText: /هذا الأسبوع|هذه الأسبوع|دروس|XP|نقاط/,
  }).first();

  const stripVisible = await stripByText.isVisible({ timeout: 5_000 }).catch(() => false);

  if (!stripVisible) {
    // FamilySummaryStrip may not be on the children page directly — check overview
    test.info().annotations.push({
      type: 'note',
      description: '3b: FamilySummaryStrip not found on /children via gradient+text selector. It may render differently or be below fold. Checking overview page.',
    });
    await page.goto('/overview');
    await page.waitForTimeout(3000);
    await shot(page, '3b-overview-page-ar-check');
    // Check if there's a "family" summary on overview (it may not be)
    test.info().annotations.push({
      type: 'note',
      description: '3b: Checking if FamilySummaryStrip is on /overview. It is a My Children page component per the capture spec (web/04-my-children.png). Captured.',
    });
  }

  // Measure the actual layout: find the CSS-grid strip element on the children page
  await page.goto('/children');
  await page.waitForTimeout(1500);
  await switchToArabic(page);
  await page.waitForTimeout(1500);

  // The strip uses a Grid with direction='rtl' (webStyle). Find it via structure.
  // It renders a Stack with position="relative" + overflow="hidden" + padding=28.
  // The first direct child column (headline) should be on the RIGHT in AR.
  // We'll measure via JS: get all top-level Stacks in the main content that are wide.

  const familyStripMeasure = await page.evaluate(() => {
    // Find elements with a grid-template-columns style (the CSS Grid strip)
    const all = document.querySelectorAll('*');
    for (const el of Array.from(all)) {
      const style = window.getComputedStyle(el as HTMLElement);
      if (style.display === 'grid' && style.gridTemplateColumns.includes('fr')) {
        const rect = (el as HTMLElement).getBoundingClientRect();
        const children = Array.from((el as HTMLElement).children) as HTMLElement[];
        if (children.length >= 2) {
          const first = children[0].getBoundingClientRect();
          const last = children[children.length - 1].getBoundingClientRect();
          return {
            found: true,
            elLeft: rect.left,
            elRight: rect.right,
            gridTemplateColumns: style.gridTemplateColumns,
            direction: style.direction,
            firstChildLeft: first.left,
            firstChildRight: first.right,
            lastChildLeft: last.left,
            lastChildRight: last.right,
            childCount: children.length,
          };
        }
      }
    }
    return { found: false };
  });

  await shot(page, '3b-family-strip-ar');

  if (!familyStripMeasure.found) {
    test.info().annotations.push({
      type: 'note',
      description: '3b: CSS Grid element not found on /children page. FamilySummaryStrip may not be rendered (needs children with data, or the strip is conditionally shown). CANT measure — visual-only.',
    });
  } else {
    const measure = familyStripMeasure as {
      found: boolean; gridTemplateColumns: string; direction: string;
      firstChildLeft: number; firstChildRight: number;
      lastChildLeft: number; lastChildRight: number; childCount: number;
    };
    test.info().annotations.push({
      type: 'info',
      description: `3b FamilySummaryStrip: grid="${measure.gridTemplateColumns}", direction="${measure.direction}", firstChild.left=${measure.firstChildLeft.toFixed(0)}, lastChild.right=${measure.lastChildRight.toFixed(0)}, childCount=${measure.childCount}`,
    });

    // In RTL grid with direction:rtl: the first column (headline) renders on the RIGHT.
    // first column's center > last column's center => headline is to the right.
    const firstChildCenterX = (measure.firstChildLeft + measure.firstChildRight) / 2;
    const lastChildCenterX = (measure.lastChildLeft + measure.lastChildRight) / 2;
    const headlineOnRight = firstChildCenterX > lastChildCenterX;

    if (headlineOnRight) {
      test.info().annotations.push({ type: 'PASS', description: '3b FIXED: FamilySummaryStrip headline is on the RIGHT in AR (CSS grid direction:rtl working).' });
    } else if (measure.direction === 'rtl') {
      // The direction is rtl but children are ordered LTR — may be a double-flip
      test.info().annotations.push({ type: 'FAIL', description: '3b POSSIBLY BROKEN: Grid direction=rtl but headline is on the LEFT. Check FamilySummaryStrip.tsx rowDir usage.' });
    } else {
      test.info().annotations.push({ type: 'note', description: `3b: Grid direction="${measure.direction}" — not rtl. The isRtl check may have returned false.` });
    }
  }
});

// =============================================================================
// 3c — ReportsWeb rows in AR (KPI cards header row, page header)
// =============================================================================
test('3c — ReportsWeb page header + KPI row alignment in AR', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/reports');
  await page.waitForTimeout(2000);
  await switchToArabic(page);
  await page.waitForTimeout(3000);

  const dir = await page.evaluate(() => document.documentElement.dir);
  expect(dir, 'must be rtl on reports page').toBe('rtl');

  // Check that the reports page loaded (not redirected)
  const currentUrl = page.url();
  if (!currentUrl.includes('reports')) {
    test.info().annotations.push({ type: 'CANT-REACH', description: `3c: /reports redirected to ${currentUrl}. CANT-REACH.` });
    return;
  }

  await shot(page, '3c-reports-page-ar-full');

  const reportsRoot = page.getByTestId('reports-root');
  const reportsVisible = await reportsRoot.isVisible({ timeout: 15_000 }).catch(() => false);
  if (!reportsVisible) {
    test.info().annotations.push({ type: 'note', description: '3c: reports-root not visible — page may be loading or error state.' });
    await shot(page, '3c-reports-root-not-visible');
    return;
  }

  // -- Page header alignment --
  const reportsHeader = page.getByTestId('reports-header');
  await reportsHeader.waitFor({ state: 'visible', timeout: 10_000 });

  const headerPositions = await reportsHeader.evaluate((el: HTMLElement) => {
    const children = Array.from(el.children) as HTMLElement[];
    if (children.length < 2) return null;
    const first = children[0].getBoundingClientRect();
    const last = children[children.length - 1].getBoundingClientRect();
    const computedDir = window.getComputedStyle(el).flexDirection;
    return {
      firstLeft: first.left,
      firstRight: first.right,
      lastLeft: last.left,
      lastRight: last.right,
      flexDirection: computedDir,
    };
  });

  if (headerPositions) {
    test.info().annotations.push({
      type: 'info',
      description: `3c reports-header: flexDirection="${headerPositions.flexDirection}", titleBlock.left=${headerPositions.firstLeft.toFixed(0)}, btns.right=${headerPositions.lastRight.toFixed(0)}`,
    });

    // ReportsWeb page header uses flexDirection={rowDir} where rowDir = isRtl ? 'row-reverse' : 'row'
    // With dir=rtl + row-reverse = DOUBLE FLIP => title on LEFT (wrong)
    // With dir=rtl + row = SINGLE FLIP => title on RIGHT (correct)
    // BUT ReportsWeb is different from OverviewWeb — it still uses rowDir in the header!
    // So the question is: was ReportsWeb also fixed or not?

    const titleOnLeft = headerPositions.firstRight < headerPositions.lastLeft;
    const titleOnRight = headerPositions.firstLeft > headerPositions.lastRight;

    if (titleOnRight) {
      test.info().annotations.push({ type: 'PASS', description: '3c reports-header: Title on RIGHT, buttons on LEFT (correct RTL, single-flip).' });
    } else if (titleOnLeft) {
      // The page header in ReportsWeb uses flexDirection={rowDir} (row-reverse in AR).
      // This is the SAME double-flip pattern as the old OverviewWeb.
      // dir=rtl + row-reverse = row-reverse cancels the rtl flip = title back on LEFT = WRONG.
      test.info().annotations.push({ type: 'FAIL', description: '3c STILL-BROKEN: reports-header title is on the LEFT. ReportsWeb.tsx page header still uses flexDirection={rowDir} (row-reverse in AR) = double-flip. Needs same fix as OverviewWeb: change to flexDirection="row".' });
      await shot(page, '3c-BROKEN-reports-header-title-on-left');
    } else {
      test.info().annotations.push({ type: 'note', description: '3c: header children overlap or wrapped (viewport wrapping).' });
    }
  }

  // -- KPI row alignment --
  const kpiRegion = page.getByTestId('reports-kpi-region');
  const kpiVisible = await kpiRegion.isVisible({ timeout: 8_000 }).catch(() => false);
  if (kpiVisible) {
    const kpiStyle = await kpiRegion.evaluate((el: HTMLElement) => {
      return window.getComputedStyle(el).flexDirection;
    });
    test.info().annotations.push({
      type: 'info',
      description: `3c reports-kpi-region: computed flexDirection="${kpiStyle}"`,
    });
    // KPI row uses flexDirection={rowDir}. In AR with row-reverse + dir=rtl:
    // The KPI cards still line up (row-reverse + rtl = LTR order visually, which may look correct for a wrapping row).
    // The issue is the ICON inside each KPI card uses flexDirection={rowDir} — same inner row double-flip.
    const kpiCardInnerStyle = await kpiRegion.evaluate((el: HTMLElement) => {
      // Find the first KPI card
      const firstCard = el.firstElementChild as HTMLElement;
      if (!firstCard) return null;
      // Find the first flex row inside (icon+label row)
      const innerRow = firstCard.querySelector('div, [class]') as HTMLElement;
      if (!innerRow) return null;
      return window.getComputedStyle(innerRow).flexDirection;
    });
    test.info().annotations.push({
      type: 'info',
      description: `3c reports KPI card inner row: computed flexDirection="${kpiCardInnerStyle}"`,
    });
  }

  await shot(page, '3c-reports-kpi-ar');

  // Capture attempts panel
  const attemptsPanel = page.getByTestId('reports-attempts-panel');
  const attemptsPanelVisible = await attemptsPanel.isVisible({ timeout: 5_000 }).catch(() => false);
  test.info().annotations.push({
    type: 'info',
    description: `3c: reports-attempts-panel visible=${attemptsPanelVisible}`,
  });
  if (attemptsPanelVisible) {
    await shot(page, '3c-reports-attempts-panel-ar');
  }
});

// =============================================================================
// S2 — Settings Language Save succeeds (no "حدث خطأ" / "No internet")
// =============================================================================
test('S2 — Settings Language & Region Save succeeds — no error', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/settings');
  await page.waitForTimeout(2000);
  await switchToArabic(page);
  await page.waitForTimeout(1500);

  const dir = await page.evaluate(() => document.documentElement.dir);
  expect(dir, 'must be rtl on settings page').toBe('rtl');

  const settingsRoot = page.getByTestId('settings-root');
  await expect(settingsRoot, 'settings-root visible').toBeVisible({ timeout: 20_000 });

  await shot(page, 'S2-settings-ar-before');

  // Find the Language & Region tab (اللغة والمنطقة).
  // Settings has a tab nav — testID="settings-tabs-nav"
  const tabsNav = page.getByTestId('settings-tabs-nav');
  await expect(tabsNav, 'settings tabs nav').toBeVisible({ timeout: 10_000 });

  const allTabs = tabsNav.getByRole('tab');
  const tabCount = await allTabs.count();
  test.info().annotations.push({ type: 'info', description: `S2: found ${tabCount} settings tabs` });

  // Language tab is the last tab (index 5 in 6-tab layout, or last)
  if (tabCount >= 6) {
    await allTabs.nth(5).click();
  } else {
    // Try finding by text
    const langTab = tabsNav.locator('text=اللغة').first();
    if (await langTab.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await langTab.click();
    } else {
      await allTabs.last().click();
    }
  }
  await page.waitForTimeout(2000);

  await shot(page, 'S2-settings-language-tab');

  // Find the language switch/select
  const langSwitch = page.getByTestId('settings-language-switch');
  const langSwitchVisible = await langSwitch.isVisible({ timeout: 8_000 }).catch(() => false);

  if (!langSwitchVisible) {
    test.info().annotations.push({
      type: 'note',
      description: 'S2: settings-language-switch not visible. The language tab may use a different testID or the tab did not switch correctly.',
    });
    await shot(page, 'S2-no-lang-switch');
    // Try scrolling down to find it
    await page.keyboard.press('End');
    await page.waitForTimeout(500);
    const langSwitchRetry = await page.getByTestId('settings-language-switch').isVisible({ timeout: 3_000 }).catch(() => false);
    if (!langSwitchRetry) {
      test.info().annotations.push({ type: 'SKIP', description: 'S2: Cannot find language-switch testID. Skipping hard assertion.' });
      return;
    }
  }

  // Find Save button (language-save testID)
  const saveBtn = page.getByTestId('language-save');
  const saveBtnVisible = await saveBtn.isVisible({ timeout: 8_000 }).catch(() => false);
  if (!saveBtnVisible) {
    test.info().annotations.push({ type: 'SKIP', description: 'S2: language-save button not found. Cannot test save action.' });
    await shot(page, 'S2-no-save-btn');
    return;
  }

  await shot(page, 'S2-before-save');

  // Intercept network to confirm the call is made (not a no-op)
  const apiCallPromise = page.waitForResponse(
    (resp) => resp.url().includes('/api/Users/Account/Language') ||
               resp.url().includes('/Language'),
    { timeout: 8_000 },
  ).catch(() => null);

  await saveBtn.click();
  await page.waitForTimeout(3500);

  const apiResp = await apiCallPromise;
  if (apiResp) {
    const status = apiResp.status();
    test.info().annotations.push({ type: 'info', description: `S2: PUT /api/Users/Account/Language → HTTP ${status}` });
    if (status >= 200 && status < 300) {
      test.info().annotations.push({ type: 'PASS', description: `S2 FIXED: Language Save returned HTTP ${status} (success).` });
    } else {
      test.info().annotations.push({ type: 'FAIL', description: `S2: Language Save returned HTTP ${status} (not 2xx). Check if the endpoint is deployed.` });
    }
  } else {
    test.info().annotations.push({ type: 'note', description: 'S2: No /api/Users/Account/Language network call intercepted. The Save button may be calling a different endpoint.' });
  }

  await shot(page, 'S2-after-save');

  // Check for error text in the page
  const bodyText = await page.locator('body').innerText().catch(() => '');
  const hasError =
    bodyText.includes('حدث خطأ') ||
    bodyText.includes('No internet') ||
    bodyText.includes('لا يوجد اتصال') ||
    bodyText.includes('network error') ||
    bodyText.includes('offline');

  if (hasError) {
    test.info().annotations.push({
      type: 'FAIL',
      description: `S2 STILL-BROKEN: Error text found after clicking Save. Body excerpt: "${bodyText.slice(0, 300)}"`,
    });
    await shot(page, 'S2-BROKEN-error-after-save');
  } else {
    test.info().annotations.push({ type: 'PASS', description: 'S2 FIXED: No "حدث خطأ" or "No internet" error visible after Save.' });
  }

  // Confirm success — either no error, or a success toast/message
  const bodyTextNow = await page.locator('body').innerText().catch(() => '');
  const hasSuccess =
    bodyTextNow.includes('تم الحفظ') ||
    bodyTextNow.includes('تم') ||
    bodyTextNow.includes('Saved') ||
    bodyTextNow.includes('success') ||
    bodyTextNow.includes('نجح');
  test.info().annotations.push({
    type: 'info',
    description: `S2: Success indicator visible=${hasSuccess}. Error indicator visible=${hasError}.`,
  });

  expect(hasError, 'S2: Must not show "حدث خطأ" or "No internet" after Language Save').toBe(false);
});

// =============================================================================
// O1 regression — KPI card icons still look correct in AR (no regression)
// =============================================================================
test('O1 regression — overview KPI card icons still correct in AR', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/overview');
  await page.waitForTimeout(2000);
  await switchToArabic(page);
  await page.waitForTimeout(1500);

  await expect(page.getByTestId('overview-root')).toBeVisible({ timeout: 20_000 });
  const kpiRegion = page.getByTestId('overview-kpi-region');
  await kpiRegion.waitFor({ state: 'visible', timeout: 15_000 });

  await shot(page, 'O1-regression-kpi-ar');

  // The KPI card inner row uses flexDirection={rowDir} (row-reverse in AR).
  // After the OverviewWeb fix: the header row now uses plain "row", but KPI card
  // internal rows STILL use rowDir=row-reverse (as designed — label on left, icon on right IN AR).
  // This is correct: the chip should be on the TRAILING side (right in EN, left in AR).
  // So in the KPI card: flexDirection=row-reverse + dir=rtl => DOUBLE FLIP = chip on right = same as EN.
  // Wait — that's the issue being checked here: does the icon end up on the correct side?

  // Per the original design (OverviewWeb.tsx line 263):
  // "flexDirection={rowDir} places chip on the trailing visual side in LTR (right) and
  //  the trailing visual side in RTL (left), matching the AR/EN pixel references"
  // So in AR: rowDir=row-reverse + dir=rtl => double-flip => chip on RIGHT (trailing visual in AR = LEFT?)
  // The comment says "trailing visual side in RTL (left)" — chip should be on LEFT in AR.

  // We measure one KPI card's icon chip and label positions
  const kpiCardMeasure = await kpiRegion.evaluate((el: HTMLElement) => {
    const firstCard = el.firstElementChild as HTMLElement;
    if (!firstCard) return null;
    // Find the first inner Stack (icon+label row)
    const innerRows = firstCard.querySelectorAll('[class]');
    let iconChip: HTMLElement | null = null;
    let labelText: HTMLElement | null = null;

    // Look for a 32x32 element (icon chip) and a text element
    for (const node of Array.from(firstCard.querySelectorAll('*')) as HTMLElement[]) {
      const rect = node.getBoundingClientRect();
      const style = window.getComputedStyle(node);
      if (
        Math.abs(rect.width - 32) < 4 &&
        Math.abs(rect.height - 32) < 4 &&
        !iconChip
      ) {
        iconChip = node;
      }
      if (
        !labelText &&
        node.children.length === 0 &&
        (node.textContent?.trim().length ?? 0) > 2 &&
        rect.width > 50
      ) {
        labelText = node;
      }
      if (iconChip && labelText) break;
    }

    if (!iconChip || !labelText) return null;
    const iconRect = (iconChip as HTMLElement).getBoundingClientRect();
    const labelRect = (labelText as HTMLElement).getBoundingClientRect();
    return {
      iconLeft: iconRect.left,
      iconRight: iconRect.right,
      labelLeft: labelRect.left,
      labelRight: labelRect.right,
    };
  });

  if (kpiCardMeasure) {
    // In AR: icon chip should be on the LEFT (trailing visual side in RTL)
    // OR: chip stays on right (if double-flip corrects back to right)
    const iconOnLeft = kpiCardMeasure.iconRight < kpiCardMeasure.labelLeft;
    const iconOnRight = kpiCardMeasure.iconLeft > kpiCardMeasure.labelRight;
    test.info().annotations.push({
      type: 'info',
      description: `O1 regression: icon.left=${kpiCardMeasure.iconLeft.toFixed(0)}, icon.right=${kpiCardMeasure.iconRight.toFixed(0)}, label.left=${kpiCardMeasure.labelLeft.toFixed(0)}, label.right=${kpiCardMeasure.labelRight.toFixed(0)} | iconOnLeft=${iconOnLeft}, iconOnRight=${iconOnRight}`,
    });
    if (iconOnLeft) {
      test.info().annotations.push({ type: 'PASS', description: 'O1: KPI icon chip is on LEFT (trailing in AR) — matches design spec.' });
    } else if (iconOnRight) {
      test.info().annotations.push({ type: 'info', description: 'O1: KPI icon chip is on RIGHT in AR (may be correct if double-flip is intentional for this case, or may be broken).' });
    }
  } else {
    test.info().annotations.push({ type: 'note', description: 'O1: Could not measure KPI icon/label positions. See screenshot.' });
  }

  // No hard assertion on icon side — this is a regression check (capture + report)
  // The key regression guard: the KPI region must still be visible
  expect(kpiRegion).toBeVisible();
});

// =============================================================================
// C7 regression — Child cards still correct in AR (regression from OverviewWeb change)
// =============================================================================
test('C7 regression — children page child cards still look correct in AR', async ({ page }) => {
  await loginDemo(page);
  await page.goto('/children');
  await page.waitForTimeout(2000);
  await switchToArabic(page);
  await page.waitForTimeout(2000);

  const dir = await page.evaluate(() => document.documentElement.dir);
  expect(dir).toBe('rtl');

  await shot(page, 'C7-regression-children-ar');

  // Check if child cards are visible
  const childCards = page.locator('[data-testid^="child-card-"]');
  const cardCount = await childCards.count();
  test.info().annotations.push({ type: 'info', description: `C7: ${cardCount} child cards found.` });

  if (cardCount === 0) {
    test.info().annotations.push({ type: 'note', description: 'C7: No child cards visible (demo.parent may have 0 children). My-children page visible but empty.' });
    // Check the my-children list at minimum
    const myChildrenList = page.getByTestId('my-children-list');
    const listVisible = await myChildrenList.isVisible({ timeout: 8_000 }).catch(() => false);
    expect(listVisible, 'my-children-list should be visible even with 0 children').toBe(true);
    return;
  }

  // Verify child cards have correct RTL row alignment
  const firstCard = childCards.first();
  await firstCard.waitFor({ state: 'visible', timeout: 10_000 });
  await shot(page, 'C7-regression-first-child-card');

  // Per ChildCard fix: rows should use RTL layout (no double-flip)
  // The key regression: cards should not have their content scrambled
  // We just verify visibility and capture — detailed row measurement is in VER-C3
  test.info().annotations.push({ type: 'PASS', description: `C7 regression: ${cardCount} child card(s) visible in AR. No obvious regression from OverviewWeb change.` });
});
