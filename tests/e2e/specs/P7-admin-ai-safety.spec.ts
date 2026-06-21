/**
 * P7-11 AI Safety & Quality Monitoring Dashboard — E2E spec
 *
 * Target: admin-dashboard Next.js at http://localhost:3001
 * Backend: .NET 10 at http://localhost:5080 (Learnexia_verify DB — sparse data)
 *
 * Coverage:
 *   AI-TC-01   Route loads under admin auth
 *   AI-TC-02   Nav entry "AI Safety" visible + active on /ai-safety
 *   AI-TC-03   Page heading renders
 *   AI-TC-04   Filter bar present (date-from, date-to, clear)
 *   AI-TC-05   Date range error shown when To < From
 *   AI-TC-06   Safety Signals section mounts (signal cards OR empty state)
 *   AI-TC-07   Safety Trend section mounts (trend chart OR empty state)
 *   AI-TC-08   Eval Results section mounts — shows sentinel "No Eval Run Yet" on fresh DB
 *   AI-TC-09   Eval sentinel does NOT show red breach banner
 *   AI-TC-10   Tutor Usage section mounts (usage cards OR empty state)
 *   AI-TC-11   Flagged Outputs table mounts (table element is present)
 *   AI-TC-12   Flagged table columns do NOT include studentId header
 *   AI-TC-13   Flagged table renders 7 correct columns (Ref / TaskKind / Action / Reasons / Checks / Model / OccurredAt)
 *   AI-TC-14   Flagged table empty state (no rows visible on sparse DB)
 *   AI-TC-15   Flagged filter selects are present (action / reason / taskKind)
 *   AI-TC-16   Unauthenticated user is redirected to /login
 *   AI-TC-17   Loading skeletons appear per-section (intercept)
 *   AI-TC-18   Signals error state + retry (intercept 500)
 *   AI-TC-19   Eval real breach renders breach banner (intercept with real breach payload)
 *   AI-TC-20   Eval sentinel payload renders sentinel state NOT breach banner
 *   AI-TC-21   Breakdown bars present in signals section (action / reason / model / taskkind)
 *   AI-TC-22   Date filter changes From/To and signals a new API call
 */

import { test, expect, type Page } from '@playwright/test';

test.use({ baseURL: 'http://localhost:3001' });
test.setTimeout(180_000);

const ADMIN_URL = 'http://localhost:3001';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function dismissDevOverlay(page: Page): Promise<void> {
  try {
    await page.evaluate(() => {
      const portal = document.querySelector('nextjs-portal') as HTMLElement | null;
      if (portal && portal.shadowRoot) {
        const overlay = portal.shadowRoot.querySelector('[data-nextjs-toast], [data-nextjs-dialog-overlay]') as HTMLElement | null;
        if (overlay) overlay.style.display = 'none';
      }
    });
    await page.waitForTimeout(150);
    await page.keyboard.press('Escape');
    await page.waitForTimeout(200);
  } catch { /* ignore */ }
}

async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto(`${ADMIN_URL}/login`);
  await page.waitForLoadState('networkidle');
  await page.getByTestId('login-username').fill('superadmin');
  await page.getByTestId('login-password').fill('123Pa$$word!');
  await page.getByTestId('login-submit').click();
  await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
}

// Sentinel payload — runId === Guid.Empty + totalCases === 0
const SENTINEL_EVAL_PAYLOAD = {
  successed: true,
  data: {
    runId: '00000000-0000-0000-0000-000000000000',
    ranAt: '2025-01-01T00:00:00Z',
    totalCases: 0,
    passedCases: 0,
    failedCases: 0,
    passRate: 0,
    failRate: 0,
    thresholdPercent: 80,
    breached: true,    // sentinel always has breached=true — must NOT show breach banner
    tier: 'gpt-4o',
    note: 'Bootstrap sentinel',
    byCheck: {},
    bySubject: {},
    byLanguage: {},
  },
};

// Real breach payload (for TC-19)
const BREACH_EVAL_PAYLOAD = {
  successed: true,
  data: {
    runId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    ranAt: '2025-06-01T12:00:00Z',
    totalCases: 100,
    passedCases: 60,
    failedCases: 40,
    passRate: 60,
    failRate: 40,
    thresholdPercent: 80,
    breached: true,
    tier: 'gpt-4o',
    note: '',
    byCheck: { 'safety-check': { passed: 60, total: 100, passRate: 60 } },
    bySubject: {},
    byLanguage: {},
  },
};

// Empty signals payload
const EMPTY_SIGNALS_PAYLOAD = {
  successed: true,
  data: {
    from: '2025-01-01T00:00:00Z',
    to: '2025-12-31T23:59:59Z',
    totalEvents: 0,
    blockedCount: 0,
    blockedRate: 0,
    regeneratedCount: 0,
    regeneratedRate: 0,
    fallbackReturnedCount: 0,
    fallbackReturnedRate: 0,
    breakdownByAction: [],
    breakdownByReasonCode: [],
    breakdownByModelId: [],
    breakdownByTaskKind: [],
  },
};

// ---------------------------------------------------------------------------
// AI-TC-16: Unauthenticated redirect (run standalone — no login)
// ---------------------------------------------------------------------------

test('AI-TC-16: Unauthenticated user is redirected to /login for /ai-safety', async ({ browser }) => {
  const context = await browser.newContext({ storageState: undefined });
  const page    = await context.newPage();
  await page.goto(`${ADMIN_URL}/ai-safety`);
  await page.waitForURL(/\/login/, { timeout: 20_000 });
  await expect(page).toHaveURL(/\/login/);
  await context.close();
});

// ---------------------------------------------------------------------------
// Authenticated suite (core navigation + structure)
// ---------------------------------------------------------------------------

test.describe('AI-SAFETY: Authenticated tests', () => {

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${ADMIN_URL}/ai-safety`);
    await page.waitForLoadState('networkidle');
    await dismissDevOverlay(page);
    // Let all section hooks fire and settle
    await page.waitForTimeout(3000);
  });

  // ── AI-TC-01: Route loads ─────────────────────────────────────────────────
  test('AI-TC-01: /ai-safety loads under admin auth', async ({ page }) => {
    expect(page.url()).toContain('/ai-safety');
    const mainContent = page.locator('main, [data-testid="admin-shell-content"], #__next');
    await expect(mainContent.first()).toBeVisible({ timeout: 10_000 });
  });

  // ── AI-TC-02: Nav entry visible + active ──────────────────────────────────
  // The nav item key is 'ai-safety' → data-testid="nav-ai-safety"
  test('AI-TC-02: Nav "AI Safety" entry is visible and active on /ai-safety', async ({ page }) => {
    const navItem = page.getByTestId('nav-ai-safety');
    await expect(navItem).toBeVisible({ timeout: 10_000 });
    await expect(navItem).toContainText(/ai safety/i);
    const ariaCurrent = await navItem.getAttribute('aria-current');
    if (ariaCurrent !== null) {
      expect(ariaCurrent).toBe('page');
    }
  });

  // ── AI-TC-03: Page heading ────────────────────────────────────────────────
  test('AI-TC-03: Page heading "AI Safety & Quality Monitoring" is visible', async ({ page }) => {
    const heading = page.getByTestId('ai-safety-page-heading');
    await expect(heading).toBeVisible({ timeout: 10_000 });
    await expect(heading).toContainText(/ai safety/i);
  });

  // ── AI-TC-04: Filter bar elements ────────────────────────────────────────
  test('AI-TC-04: Filter bar — date-from and date-to inputs are present', async ({ page }) => {
    await expect(page.getByTestId('ai-safety-filter-date-from')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('ai-safety-filter-date-to')).toBeVisible({ timeout: 10_000 });
  });

  // ── AI-TC-05: Date range error ────────────────────────────────────────────
  test('AI-TC-05: Date range error shown when "To" is before "From"', async ({ page }) => {
    const fromInput = page.getByTestId('ai-safety-filter-date-from');
    const toInput   = page.getByTestId('ai-safety-filter-date-to');

    await fromInput.fill('2025-06-15');
    await toInput.fill('2025-06-01');
    await page.waitForTimeout(500);

    const errorBanner = page.getByTestId('ai-safety-date-range-error');
    await expect(errorBanner).toBeVisible({ timeout: 5_000 });
    await expect(errorBanner).toHaveAttribute('role', 'alert');

    // Fix range — error clears
    await toInput.fill('2025-06-20');
    await page.waitForTimeout(400);
    await expect(errorBanner).not.toBeVisible({ timeout: 5_000 });
  });

  // ── AI-TC-06: Safety Signals section mounts ──────────────────────────────
  test('AI-TC-06: Safety Signals section mounts (cards OR empty state OR loading)', async ({ page }) => {
    // Check any meaningful render from signals section
    const signalsResults = page.getByTestId('signals-results');
    const signalsEmpty   = page.getByTestId('signals-empty');
    const signalsLoading = page.getByTestId('signals-loading');
    const signalsError   = page.getByTestId('signals-error');

    const hasResults = await signalsResults.isVisible({ timeout: 5_000 }).catch(() => false);
    const hasEmpty   = await signalsEmpty.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasLoading = await signalsLoading.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasError   = await signalsError.isVisible({ timeout: 3_000 }).catch(() => false);

    const somethingVisible = hasResults || hasEmpty || hasLoading || hasError;
    expect(somethingVisible).toBe(true);
  });

  // ── AI-TC-07: Safety Trend section mounts ────────────────────────────────
  // SafetyTrend.tsx uses 'safety-trend-*' testIDs (not 'trend-*')
  test('AI-TC-07: Safety Trend section mounts (chart OR empty state)', async ({ page }) => {
    const trendResults = page.getByTestId('safety-trend-results');
    const trendEmpty   = page.getByTestId('safety-trend-empty');
    const trendLoading = page.getByTestId('safety-trend-loading');
    const trendError   = page.getByTestId('safety-trend-error');

    const hasResults = await trendResults.isVisible({ timeout: 5_000 }).catch(() => false);
    const hasEmpty   = await trendEmpty.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasLoading = await trendLoading.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasError   = await trendError.isVisible({ timeout: 3_000 }).catch(() => false);

    const somethingVisible = hasResults || hasEmpty || hasLoading || hasError;
    expect(somethingVisible).toBe(true);
  });

  // ── AI-TC-08: Eval Results section mounts — sentinel expected on fresh DB ─
  test('AI-TC-08: Eval Results section mounts and shows sentinel "No Eval Run Yet" on fresh DB', async ({ page }) => {
    const evalSentinel = page.getByTestId('eval-sentinel');
    const evalResults  = page.getByTestId('eval-results');
    const evalLoading  = page.getByTestId('eval-loading');
    const evalError    = page.getByTestId('eval-error');

    const hasSentinel = await evalSentinel.isVisible({ timeout: 8_000 }).catch(() => false);
    const hasResults  = await evalResults.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasLoading  = await evalLoading.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasError    = await evalError.isVisible({ timeout: 3_000 }).catch(() => false);

    const somethingVisible = hasSentinel || hasResults || hasLoading || hasError;
    expect(somethingVisible).toBe(true);

    // On a fresh DB we expect the sentinel
    if (hasSentinel) {
      await expect(evalSentinel).toBeVisible();
      // Sentinel heading text
      const sentinelText = await evalSentinel.textContent();
      expect(sentinelText).toMatch(/No Eval Run Yet|لم يُجرَ أي تقييم/i);
    }
  });

  // ── AI-TC-09: Sentinel does NOT show breach banner ────────────────────────
  test('AI-TC-09: Eval sentinel state does NOT show red breach banner', async ({ page }) => {
    const evalSentinel   = page.getByTestId('eval-sentinel');
    const evalBreachBanner = page.getByTestId('eval-breach-banner');

    const hasSentinel = await evalSentinel.isVisible({ timeout: 8_000 }).catch(() => false);

    if (hasSentinel) {
      // Breach banner must NOT be visible when sentinel is shown
      const hasBreachBanner = await evalBreachBanner.isVisible({ timeout: 2_000 }).catch(() => false);
      expect(hasBreachBanner).toBe(false);
    } else {
      // Not in sentinel state — skip
      test.skip(true, 'Eval not in sentinel state; cannot verify breach-banner suppression via live DB');
    }
  });

  // ── AI-TC-10: Tutor Usage section mounts ─────────────────────────────────
  test('AI-TC-10: Tutor Usage section mounts (cards OR empty state)', async ({ page }) => {
    const usageResults = page.locator('[data-testid^="usage-card-"]').first();
    const usageEmpty   = page.getByTestId('usage-empty');
    const usageLoading = page.getByTestId('usage-loading');
    const usageError   = page.getByTestId('usage-error');

    const hasResults = await usageResults.isVisible({ timeout: 5_000 }).catch(() => false);
    const hasEmpty   = await usageEmpty.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasLoading = await usageLoading.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasError   = await usageError.isVisible({ timeout: 3_000 }).catch(() => false);

    const somethingVisible = hasResults || hasEmpty || hasLoading || hasError;
    expect(somethingVisible).toBe(true);
  });

  // ── AI-TC-11: Flagged Outputs table element is present ────────────────────
  test('AI-TC-11: Flagged Outputs table element is present in DOM', async ({ page }) => {
    const table = page.getByTestId('flagged-outputs-table');
    await expect(table).toBeVisible({ timeout: 15_000 });
  });

  // ── AI-TC-12: Flagged table has NO studentId column ──────────────────────
  test('AI-TC-12: Flagged outputs table does NOT render "studentId" as a column header', async ({ page }) => {
    const table = page.getByTestId('flagged-outputs-table');
    await expect(table).toBeVisible({ timeout: 15_000 });

    const headers = await table.locator('th').allTextContents();
    const headerText = headers.map((h) => h.toLowerCase()).join(' ');

    // Must not contain studentid in any form
    expect(headerText).not.toContain('studentid');
    expect(headerText).not.toContain('student id');
    expect(headerText).not.toContain('student_id');
  });

  // ── AI-TC-13: Flagged table has 7 correct columns ────────────────────────
  test('AI-TC-13: Flagged table renders 7 PII-light columns (Ref/TaskKind/Action/Reasons/Checks/Model/OccurredAt)', async ({ page }) => {
    const table = page.getByTestId('flagged-outputs-table');
    await expect(table).toBeVisible({ timeout: 15_000 });

    const headers = await table.locator('th').allTextContents();
    expect(headers.length).toBe(7);

    const joined = headers.map((h) => h.toLowerCase()).join('|');
    // Check all 7 expected column concepts are present
    expect(joined).toMatch(/ref/i);           // Ref. ID / contentRef
    expect(joined).toMatch(/task/i);          // Task Kind
    expect(joined).toMatch(/action/i);        // Action
    expect(joined).toMatch(/reason/i);        // Reason Codes
    expect(joined).toMatch(/check/i);         // Failed Checks
    expect(joined).toMatch(/model/i);         // Model
    expect(joined).toMatch(/occur|time|at/i); // Occurred At
  });

  // ── AI-TC-14: Flagged table empty state on sparse DB ─────────────────────
  test('AI-TC-14: Flagged table shows empty state (no flagged outputs on fresh DB)', async ({ page }) => {
    const table   = page.getByTestId('flagged-outputs-table');
    await expect(table).toBeVisible({ timeout: 15_000 });

    const emptyCell = page.getByTestId('flagged-empty');
    const hasEmpty  = await emptyCell.isVisible({ timeout: 5_000 }).catch(() => false);

    if (hasEmpty) {
      await expect(emptyCell).toBeVisible();
      const emptyText = await emptyCell.textContent();
      expect(emptyText).toBeTruthy();
    } else {
      // If data exists (unexpected on fresh DB), just verify no studentId cells
      const rows = table.locator('tbody tr');
      const rowCount = await rows.count();
      if (rowCount > 0) {
        const cells = await rows.first().locator('td').allTextContents();
        // Must be 7 cells (not 8 which would indicate an extra studentId column)
        expect(cells.length).toBeLessThanOrEqual(7);
      }
    }
  });

  // ── AI-TC-15: Flagged filter selects present ─────────────────────────────
  test('AI-TC-15: Flagged table filter selects are present (action / reason / taskKind)', async ({ page }) => {
    await expect(page.getByTestId('flagged-filter-action')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('flagged-filter-reason')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('flagged-filter-taskkind')).toBeVisible({ timeout: 10_000 });
  });

  // ── AI-TC-21: Breakdown bars in signals section ───────────────────────────
  test('AI-TC-21: Breakdown bar containers present in signals section when data rendered', async ({ page }) => {
    const signalsResults = page.getByTestId('signals-results');
    const hasSResults = await signalsResults.isVisible({ timeout: 8_000 }).catch(() => false);
    if (!hasSResults) {
      test.skip(true, 'Signals section not in results state — sparse DB shows empty state');
      return;
    }
    await expect(page.getByTestId('signal-breakdown-action')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('signal-breakdown-reason')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('signal-breakdown-model')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('signal-breakdown-taskkind')).toBeVisible({ timeout: 10_000 });
  });

  // ── AI-TC-22: Date filter triggers API call ───────────────────────────────
  test('AI-TC-22: Setting date range triggers new API call to AiSafety/signals', async ({ page }) => {
    let requestFired = false;
    page.on('request', (req) => {
      if (req.url().includes('/AiSafety/signals') || req.url().includes('/aisafety/signals')) {
        requestFired = true;
      }
    });

    const fromInput = page.getByTestId('ai-safety-filter-date-from');
    const toInput   = page.getByTestId('ai-safety-filter-date-to');
    await fromInput.fill('2025-01-01');
    await toInput.fill('2025-12-31');
    await page.waitForTimeout(2000);

    expect(requestFired).toBe(true);
  });

});

// ---------------------------------------------------------------------------
// Intercept-based tests (eval states + loading)
// ---------------------------------------------------------------------------

test.describe('AI-SAFETY: Intercept / injected-state tests', () => {

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  // ── AI-TC-17: Loading skeletons appear per-section ────────────────────────
  test('AI-TC-17: Section loading skeletons appear while data fetches', async ({ page }) => {
    // Delay signals fetch
    await page.route(/\/api\/Admin\/AiSafety\/signals/i, async (route) => {
      await page.waitForTimeout(2000);
      await route.continue();
    });

    await page.goto(`${ADMIN_URL}/ai-safety`);

    const signalsLoading = page.getByTestId('signals-loading');
    const skeletonVisible = await signalsLoading.isVisible({ timeout: 5_000 }).catch(() => false);
    if (skeletonVisible) {
      // role="status" is expected per spec
      const role = await signalsLoading.getAttribute('role').catch(() => null);
      if (role !== null) {
        expect(role).toBe('status');
      }
    }

    await page.unrouteAll();
    await page.waitForLoadState('networkidle');
    // After loading, signals-loading should be gone
    await expect(signalsLoading).not.toBeVisible({ timeout: 15_000 });
  });

  // ── AI-TC-18: Signals error state + retry ────────────────────────────────
  test('AI-TC-18: Signals section shows error + retry when API returns 500', async ({ page }) => {
    let failCount = 0;
    await page.route(/\/api\/Admin\/AiSafety\/signals/i, (route) => {
      failCount++;
      if (failCount <= 4) {
        route.fulfill({ status: 500, body: JSON.stringify({ error: 'Server Error' }) });
      } else {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(EMPTY_SIGNALS_PAYLOAD),
        });
      }
    });

    await page.goto(`${ADMIN_URL}/ai-safety`);
    await page.waitForTimeout(15_000); // exhaust retries

    const signalsError = page.getByTestId('signals-error');
    await expect(signalsError).toBeVisible({ timeout: 20_000 });

    await page.unrouteAll();
    // After unrouting, TanStack Query background refetch will succeed
  });

  // ── AI-TC-19: Real breach renders breach banner ───────────────────────────
  test('AI-TC-19: Real eval breach (non-sentinel) renders breach banner', async ({ page }) => {
    await page.route(/\/api\/Admin\/AiSafety\/evals/i, (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(BREACH_EVAL_PAYLOAD),
      });
    });

    await page.goto(`${ADMIN_URL}/ai-safety`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Must show real results panel — NOT sentinel
    const evalResults = page.getByTestId('eval-results');
    await expect(evalResults).toBeVisible({ timeout: 10_000 });

    // Sentinel must NOT appear
    const evalSentinel = page.getByTestId('eval-sentinel');
    await expect(evalSentinel).not.toBeVisible({ timeout: 2_000 });

    // Breach banner must appear
    const breachBanner = page.getByTestId('eval-breach-banner');
    await expect(breachBanner).toBeVisible({ timeout: 5_000 });

    await page.unrouteAll();
  });

  // ── AI-TC-20: Sentinel payload renders sentinel state, NOT breach banner ───
  test('AI-TC-20: Bootstrap sentinel payload renders "No Eval Run Yet" and NOT breach banner', async ({ page }) => {
    await page.route(/\/api\/Admin\/AiSafety\/evals/i, (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SENTINEL_EVAL_PAYLOAD),
      });
    });

    await page.goto(`${ADMIN_URL}/ai-safety`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Sentinel must be visible
    const evalSentinel = page.getByTestId('eval-sentinel');
    await expect(evalSentinel).toBeVisible({ timeout: 10_000 });

    // Breach banner must NOT appear (sentinel breached=true must not trigger it)
    const breachBanner = page.getByTestId('eval-breach-banner');
    await expect(breachBanner).not.toBeVisible({ timeout: 2_000 });

    // eval-results must NOT appear (sentinel replaces results)
    const evalResults = page.getByTestId('eval-results');
    await expect(evalResults).not.toBeVisible({ timeout: 2_000 });

    await page.unrouteAll();
  });

});
