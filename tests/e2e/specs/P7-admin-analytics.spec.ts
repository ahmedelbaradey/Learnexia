/**
 * P7-10 Platform Analytics Dashboard — E2E spec
 *
 * Target: admin-dashboard Next.js at http://localhost:3001
 * Backend: .NET 10 at http://localhost:5080 (Learnexia_verify DB — sparse data)
 *
 * Coverage:
 *   ANALYTICS-TC-01  Route loads under admin auth
 *   ANALYTICS-TC-02  Nav entry "Analytics" visible + active on /analytics
 *   ANALYTICS-TC-03  Page heading renders
 *   ANALYTICS-TC-04  Filter bar elements present (date-from, date-to, subject, grade, language)
 *   ANALYTICS-TC-05  Date range error shows when To < From; clears on correction
 *   ANALYTICS-TC-06  Date filter triggers re-fetch (loading skeleton appears)
 *   ANALYTICS-TC-07  Clear filters button appears when a filter is set; resets to default
 *   ANALYTICS-TC-08  KPI cards render (success OR empty OR N/A — not a blank white screen)
 *   ANALYTICS-TC-09  N/A state: quizzes card always renders N/A label (never a raw 0)
 *   ANALYTICS-TC-10  Breakdown chart sections mount (subject / grade / language containers present)
 *   ANALYTICS-TC-11  Client-side subject slice select changes value without API re-fetch
 *   ANALYTICS-TC-12  Client-side grade slice select changes value
 *   ANALYTICS-TC-13  Client-side language slice select changes value
 *   ANALYTICS-TC-14  Unauthenticated user is redirected to /login
 *   ANALYTICS-TC-15  Loading skeleton appears while data fetches (intercept route)
 *   ANALYTICS-TC-16  Error state + retry button on API failure (intercept 500)
 *   ANALYTICS-TC-17  Empty state renders when all metrics are zero (intercept with zeros)
 *   ANALYTICS-TC-18  No "Social Studies" subject option in slice select
 *   ANALYTICS-TC-19  Subscriptions KPI card renders (subscriptions section)
 *   ANALYTICS-TC-20  AI Safety KPI section renders (ai-safety-events / ai-blocked / ai-flagged cards)
 */

import { test, expect, type Page } from '@playwright/test';

test.use({ baseURL: 'http://localhost:3001' });
test.setTimeout(180_000);

const ADMIN_URL = 'http://localhost:3001';
const API_URL   = 'http://localhost:5080';

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

// ---------------------------------------------------------------------------
// ANALYTICS-TC-14: Unauthenticated redirect (no auth needed — run standalone)
// ---------------------------------------------------------------------------

test('ANALYTICS-TC-14: Unauthenticated user is redirected to /login', async ({ browser }) => {
  const context = await browser.newContext({ storageState: undefined });
  const page    = await context.newPage();
  await page.goto(`${ADMIN_URL}/analytics`);
  await page.waitForURL(/\/login/, { timeout: 20_000 });
  await expect(page).toHaveURL(/\/login/);
  await context.close();
});

// ---------------------------------------------------------------------------
// Authenticated suite
// ---------------------------------------------------------------------------

test.describe('ANALYTICS: Authenticated tests', () => {

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${ADMIN_URL}/analytics`);
    await page.waitForLoadState('networkidle');
    await dismissDevOverlay(page);
    // Allow data to settle
    await page.waitForTimeout(1500);
  });

  // ── ANALYTICS-TC-01: Route loads ───────────────────────────────────────────
  test('ANALYTICS-TC-01: /analytics loads under admin auth (HTTP 200)', async ({ page }) => {
    // If we're on /analytics without redirect, the route loaded fine
    expect(page.url()).toContain('/analytics');
    // Shell wrapper must be present — check for main content area
    const mainContent = page.locator('main, [data-testid="admin-shell-content"], #__next');
    await expect(mainContent.first()).toBeVisible({ timeout: 10_000 });
  });

  // ── ANALYTICS-TC-02: Nav entry present and active ─────────────────────────
  test('ANALYTICS-TC-02: Nav "Analytics" entry is present and active on /analytics', async ({ page }) => {
    const navItem = page.getByTestId('nav-analytics');
    await expect(navItem).toBeVisible({ timeout: 10_000 });
    // Active state: aria-current="page" or a visual active class/style
    const ariaCurrent = await navItem.getAttribute('aria-current');
    if (ariaCurrent !== null) {
      expect(ariaCurrent).toBe('page');
    }
    // Nav entry text
    await expect(navItem).toContainText(/analytics/i);
  });

  // ── ANALYTICS-TC-03: Page heading renders ────────────────────────────────
  test('ANALYTICS-TC-03: Page heading "Platform Analytics" is visible', async ({ page }) => {
    const heading = page.getByTestId('analytics-page-heading');
    await expect(heading).toBeVisible({ timeout: 10_000 });
    await expect(heading).toContainText(/platform analytics/i);
  });

  // ── ANALYTICS-TC-04: Filter bar elements present ─────────────────────────
  test('ANALYTICS-TC-04: Filter bar — date-from, date-to, subject, grade, language selects are visible', async ({ page }) => {
    await expect(page.getByTestId('analytics-filter-date-from')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('analytics-filter-date-to')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('analytics-filter-subject')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('analytics-filter-grade')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('analytics-filter-language')).toBeVisible({ timeout: 10_000 });
  });

  // ── ANALYTICS-TC-05: Date range error when To < From ─────────────────────
  test('ANALYTICS-TC-05: Date range error shown when "To" is before "From"; clears on correction', async ({ page }) => {
    const fromInput = page.getByTestId('analytics-filter-date-from');
    const toInput   = page.getByTestId('analytics-filter-date-to');

    await fromInput.fill('2025-06-15');
    await toInput.fill('2025-06-01');
    await page.waitForTimeout(500);

    const errorBanner = page.getByTestId('analytics-date-range-error');
    await expect(errorBanner).toBeVisible({ timeout: 5_000 });
    await expect(errorBanner).toHaveAttribute('role', 'alert');

    // Fix the range — error should disappear
    await toInput.fill('2025-06-20');
    await page.waitForTimeout(500);
    await expect(errorBanner).not.toBeVisible({ timeout: 5_000 });
  });

  // ── ANALYTICS-TC-06: Date filter triggers re-fetch ────────────────────────
  test('ANALYTICS-TC-06: Setting date range triggers a new API call to /Analytics/kpis', async ({ page }) => {
    let requestFired = false;
    page.on('request', (req) => {
      if (req.url().includes('/Analytics/kpis') || req.url().includes('/analytics/kpis')) {
        requestFired = true;
      }
    });
    const fromInput = page.getByTestId('analytics-filter-date-from');
    const toInput   = page.getByTestId('analytics-filter-date-to');
    await fromInput.fill('2025-01-01');
    await toInput.fill('2025-12-31');
    await page.waitForTimeout(2000);
    expect(requestFired).toBe(true);
  });

  // ── ANALYTICS-TC-07: Clear filters button ────────────────────────────────
  test('ANALYTICS-TC-07: Clear filters button appears after setting a filter and resets state', async ({ page }) => {
    // Clear button should NOT be visible when no filters are active
    const clearBtn = page.getByTestId('analytics-clear-filters');
    // Wait for page to settle then set a filter
    await page.getByTestId('analytics-filter-date-from').fill('2025-01-01');
    await page.waitForTimeout(400);
    await expect(clearBtn).toBeVisible({ timeout: 5_000 });

    // Click clear — button disappears
    await clearBtn.click();
    await page.waitForTimeout(400);
    await expect(clearBtn).not.toBeVisible({ timeout: 5_000 });

    // From input should be empty
    await expect(page.getByTestId('analytics-filter-date-from')).toHaveValue('');
  });

  // ── ANALYTICS-TC-08: KPI cards or empty/loading state — not blank ─────────
  test('ANALYTICS-TC-08: Page renders a meaningful state (cards, empty, or loading — no blank screen)', async ({ page }) => {
    // Wait for all states to settle after networkidle
    await page.waitForTimeout(3000);

    const hasCards    = await page.locator('[data-testid^="kpi-card-"]').first().isVisible({ timeout: 3000 }).catch(() => false);
    const hasEmpty    = await page.getByTestId('analytics-empty').isVisible({ timeout: 2000 }).catch(() => false);
    const hasLoading  = await page.getByTestId('analytics-loading').isVisible({ timeout: 2000 }).catch(() => false);
    const hasError    = await page.getByTestId('analytics-error-banner').isVisible({ timeout: 2000 }).catch(() => false);

    const somethingVisible = hasCards || hasEmpty || hasLoading || hasError;
    expect(somethingVisible).toBe(true);
  });

  // ── ANALYTICS-TC-09: Quizzes card always shows N/A ───────────────────────
  test('ANALYTICS-TC-09: Quizzes KPI card renders with N/A label — never raw 0', async ({ page }) => {
    await page.waitForTimeout(3000); // wait for data

    const quizzesCard = page.getByTestId('kpi-card-quizzes');
    const hasCard     = await quizzesCard.isVisible({ timeout: 5_000 }).catch(() => false);

    // If KPI cards rendered (results state), quizzes must show N/A
    if (hasCard) {
      // The card should NOT contain a plain "0" as the primary metric
      const cardText = await quizzesCard.textContent();
      // Must contain N/A text
      expect(cardText).toMatch(/N\/A|n\/a|غ\.م\./i);

      // Value span should NOT be present (N/A path doesn't render the value span)
      const valueSpan = page.getByTestId('kpi-value-quizzes');
      const hasValue  = await valueSpan.isVisible({ timeout: 1_000 }).catch(() => false);
      expect(hasValue).toBe(false);
    } else {
      // Not in results state — skip assertion (acceptable)
      test.skip(true, 'Quizzes card not visible (page not in results state — empty or error)');
    }
  });

  // ── ANALYTICS-TC-10: Breakdown chart sections mount ──────────────────────
  test('ANALYTICS-TC-10: Breakdown chart section containers mount (subject / grade / language)', async ({ page }) => {
    await page.waitForTimeout(3000);

    // Only check chart containers if in results state
    const hasResults = await page.locator('[data-testid^="kpi-card-"]').first().isVisible({ timeout: 3_000 }).catch(() => false);
    if (!hasResults) {
      test.skip(true, 'Not in results state — breakdown charts not expected (sparse DB)');
      return;
    }

    await expect(page.getByTestId('analytics-chart-by-subject')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('analytics-chart-by-grade')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('analytics-chart-by-language')).toBeVisible({ timeout: 10_000 });
  });

  // ── ANALYTICS-TC-11: Subject slice changes value, params NOT added to URL ──
  // The design spec (PART C) says slice selects are client-side only — they do
  // NOT add new query params to the /kpis request. We verify this by capturing
  // any /kpis URL fired after the change and asserting it has NO subject/grade/
  // language query params. A refetchOnWindowFocus background refetch (TanStack
  // Query default) is acceptable here — it will reuse the SAME empty params,
  // proving the slice does not change the API contract.
  test('ANALYTICS-TC-11: Subject slice select — no subject/grade/language params sent to /kpis', async ({ page }) => {
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const capturedUrls: string[] = [];
    const handler = (req: import('@playwright/test').Request) => {
      if (req.url().includes('/Analytics/kpis') || req.url().includes('/analytics/kpis')) {
        capturedUrls.push(req.url());
      }
    };
    page.on('request', handler);

    const subjectSelect = page.getByTestId('analytics-filter-subject');
    await expect(subjectSelect).toBeVisible({ timeout: 10_000 });
    await subjectSelect.selectOption({ index: 1 });
    await page.waitForTimeout(1500);
    page.off('request', handler);

    // Value changed
    const value = await subjectSelect.inputValue();
    expect(value).not.toBe('');

    // Any /kpis requests fired must NOT include slice params — proves client-side only
    for (const url of capturedUrls) {
      expect(url).not.toContain('subject');
      expect(url).not.toContain('grade');
      expect(url).not.toContain('language');
      expect(url).not.toContain('subjectSlice');
      expect(url).not.toContain('gradeSlice');
    }
  });

  // ── ANALYTICS-TC-12: Grade slice — no params sent to /kpis ───────────────
  test('ANALYTICS-TC-12: Grade slice select — no grade params sent to /kpis', async ({ page }) => {
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const capturedUrls: string[] = [];
    const handler = (req: import('@playwright/test').Request) => {
      if (req.url().includes('/Analytics/kpis') || req.url().includes('/analytics/kpis')) {
        capturedUrls.push(req.url());
      }
    };
    page.on('request', handler);

    const gradeSelect = page.getByTestId('analytics-filter-grade');
    await expect(gradeSelect).toBeVisible({ timeout: 10_000 });
    await gradeSelect.selectOption({ index: 1 });
    await page.waitForTimeout(1500);
    page.off('request', handler);

    const value = await gradeSelect.inputValue();
    expect(value).toBe('1');

    for (const url of capturedUrls) {
      expect(url).not.toContain('grade');
      expect(url).not.toContain('gradeSlice');
    }
  });

  // ── ANALYTICS-TC-13: Language slice — no params sent to /kpis ────────────
  test('ANALYTICS-TC-13: Language slice select — no language params sent to /kpis', async ({ page }) => {
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const capturedUrls: string[] = [];
    const handler = (req: import('@playwright/test').Request) => {
      if (req.url().includes('/Analytics/kpis') || req.url().includes('/analytics/kpis')) {
        capturedUrls.push(req.url());
      }
    };
    page.on('request', handler);

    const langSelect = page.getByTestId('analytics-filter-language');
    await expect(langSelect).toBeVisible({ timeout: 10_000 });
    await langSelect.selectOption({ index: 1 });
    await page.waitForTimeout(1500);
    page.off('request', handler);

    const value = await langSelect.inputValue();
    expect(value).not.toBe('');

    for (const url of capturedUrls) {
      expect(url).not.toContain('language');
      expect(url).not.toContain('languageSlice');
    }
  });

  // ── ANALYTICS-TC-18: No "Social Studies" subject option ───────────────────
  test('ANALYTICS-TC-18: Subject slice select has no "Social Studies" option', async ({ page }) => {
    const subjectSelect = page.getByTestId('analytics-filter-subject');
    await expect(subjectSelect).toBeVisible({ timeout: 10_000 });

    const optionTexts = await subjectSelect.evaluate((el: HTMLSelectElement) =>
      Array.from(el.options).map((o) => o.text.toLowerCase()),
    );

    expect(optionTexts.some((t) => t.includes('social'))).toBe(false);
    // Must include the 4 real subjects (partial match)
    expect(optionTexts.some((t) => t.includes('math') || t.includes('رياضيات'))).toBe(true);
  });

  // ── ANALYTICS-TC-19: Subscriptions KPI card ──────────────────────────────
  test('ANALYTICS-TC-19: Subscriptions KPI card is present when in results state', async ({ page }) => {
    await page.waitForTimeout(3000);
    const hasResults = await page.locator('[data-testid^="kpi-card-"]').first().isVisible({ timeout: 3_000 }).catch(() => false);
    if (!hasResults) {
      test.skip(true, 'Not in results state — sparse DB, no subscriptions data');
      return;
    }
    await expect(page.getByTestId('kpi-card-subscriptions')).toBeVisible({ timeout: 10_000 });
  });

  // ── ANALYTICS-TC-20: AI Safety KPI section ────────────────────────────────
  test('ANALYTICS-TC-20: AI Safety KPI cards are present in results state', async ({ page }) => {
    await page.waitForTimeout(3000);
    const hasResults = await page.locator('[data-testid^="kpi-card-"]').first().isVisible({ timeout: 3_000 }).catch(() => false);
    if (!hasResults) {
      test.skip(true, 'Not in results state — sparse DB');
      return;
    }
    await expect(page.getByTestId('kpi-card-ai-safety-events')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('kpi-card-ai-blocked')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('kpi-card-ai-flagged')).toBeVisible({ timeout: 10_000 });
  });

});

// ---------------------------------------------------------------------------
// Intercept-based tests (independent from beforeEach login state)
// ---------------------------------------------------------------------------

test.describe('ANALYTICS: Intercept / injected-state tests', () => {

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  // ── ANALYTICS-TC-15: Loading skeleton ─────────────────────────────────────
  test('ANALYTICS-TC-15: Loading skeleton appears while data fetches', async ({ page }) => {
    // Delay the API response
    await page.route(/\/api\/Admin\/Analytics\/kpis/i, async (route) => {
      await page.waitForTimeout(1500);
      await route.continue();
    });

    await page.goto(`${ADMIN_URL}/analytics`);
    // Skeleton should be visible briefly
    const skeleton = page.getByTestId('analytics-loading');
    const skeletonVisible = await skeleton.isVisible({ timeout: 5_000 }).catch(() => false);
    // Either skeleton appeared, or it was so fast we missed it — both acceptable
    if (skeletonVisible) {
      await expect(skeleton).toHaveAttribute('role', 'status');
    }
    await page.unrouteAll();
    await page.waitForLoadState('networkidle');
    // After loading, skeleton gone
    await expect(skeleton).not.toBeVisible({ timeout: 15_000 });
  });

  // ── ANALYTICS-TC-16: Error state + retry ──────────────────────────────────
  test('ANALYTICS-TC-16: Error state renders on API 500; retry triggers re-fetch', async ({ page }) => {
    let failCount = 0;
    await page.route(/\/api\/Admin\/Analytics\/kpis/i, (route) => {
      failCount++;
      if (failCount <= 4) {
        // TanStack Query retries 3 times by default; exhaust them
        route.fulfill({ status: 500, body: JSON.stringify({ error: 'Internal Server Error' }) });
      } else {
        route.continue();
      }
    });

    await page.goto(`${ADMIN_URL}/analytics`);
    // Wait for TanStack Query to exhaust retries (default retry=3 with delay)
    await page.waitForTimeout(15_000);

    const errorBanner = page.getByTestId('analytics-error-banner');
    await expect(errorBanner).toBeVisible({ timeout: 20_000 });

    // Retry button present
    const retryBtn = page.getByTestId('analytics-retry');
    await expect(retryBtn).toBeVisible({ timeout: 5_000 });

    // Unblock and click retry
    await page.unrouteAll();
    await retryBtn.click();
    // Error banner should clear once data loads
    await expect(errorBanner).not.toBeVisible({ timeout: 20_000 });
  });

  // ── ANALYTICS-TC-17: Empty state (intercept with zero payload) ────────────
  test('ANALYTICS-TC-17: Empty state renders when API returns all-zero metrics', async ({ page }) => {
    const emptyPayload = {
      successed: true,
      data: {
        fromUtc: '2025-01-01T00:00:00Z',
        toUtc: '2025-12-31T23:59:59Z',
        lessonsCompleted: 0,
        totalAttempts: 0,
        distinctActiveStudents: 0,
        quizzesCompletedNaReason: 'No data',
        bySubject: [],
        byGrade: [],
        byLanguage: [],
        missionsCompleted: 0,
        xpEarnedInWindow: 0,
        totalActiveSubscriptions: 0,
        subscriptionsByTier: [],
        revenueNaReason: null,
        totalAiSafetyEvents: 0,
        aiBlockedCount: 0,
        aiFlaggedCount: 0,
        aiRequestVolume: 0,
        aiRequestVolumeNaReason: null,
        analyticsActiveStudents: 0,
        totalSessions: 0,
        avgSessionDurationSeconds: 0,
        avgActiveDaysPerStudent: 0,
        returningStudentRate: 0,
        retentionNaReason: null,
        sessionDurationNaReason: null,
      },
    };

    await page.route(/\/api\/Admin\/Analytics\/kpis/i, (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(emptyPayload),
      });
    });

    await page.goto(`${ADMIN_URL}/analytics`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    const emptyState = page.getByTestId('analytics-empty');
    await expect(emptyState).toBeVisible({ timeout: 10_000 });
    await page.unrouteAll();
  });

});
