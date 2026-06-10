/**
 * E2E spec — Marketing site: 4 design-system components + Arabic/RTL
 *
 * Story: marketing-components-ar (ad-hoc enhancement)
 * Plan:  docs/plans/marketing-components-ar.md (E2E-01, E2E-02)
 * Brief: docs/briefs/marketing-components-ar.md
 * Design spec: design-system/ui_kits/marketing/marketing-components-ar.md (§9 RTL)
 *
 * Target: Next.js 15 marketing site on http://localhost:3002
 * Project: "marketing" / "marketing-mobile" (see playwright.config.ts)
 *
 * Groups (matching plan E2E-02 coverage table):
 *   A. Locale routing
 *   B. Language switcher
 *   C. BenefitsPanel
 *   D. ActivityChart
 *   E. AITutorBubble
 *   F. ChildCardPhone
 *   G. RTL layout at mobile width
 *   H. No console errors
 */

import { test, expect } from '@playwright/test';

// ── constants ──────────────────────────────────────────────────────────────────

/** Base URL for the marketing site; the project config already sets this but
 *  we import it here to make navigations explicit and portable when run outside
 *  a project context (e.g. `--project=chromium` by mistake). */
const BASE = process.env.MARKETING_URL ?? 'http://localhost:3002';

// Arabic-Indic digits spotted in bar value labels on /ar
const AR_INDIC_PATTERN = /[٠-٩]/u;

// ── Group A — Locale routing ───────────────────────────────────────────────────

test.describe('A. Locale routing', () => {
  test('A-01: bare / redirects to /en', async ({ page }) => {
    const response = await page.goto(`${BASE}/`, { waitUntil: 'load' });
    // After redirect the URL must be /en (or /en with a trailing slash)
    expect(page.url()).toMatch(/\/en\/?$/);
    // Must NOT stay at /
    expect(page.url()).not.toMatch(/localhost:\d+\/?$/);
    // The response chain should have included a redirect (3xx) or the final
    // status is 200 (server-side redirect via Next.js)
    expect(response?.status()).toBeLessThan(400);
  });

  test('A-02: /en sets <html lang="en" dir="ltr">', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'en');
    await expect(html).toHaveAttribute('dir', 'ltr');
  });

  test('A-03: /ar sets <html lang="ar" dir="rtl">', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'ar');
    await expect(html).toHaveAttribute('dir', 'rtl');
  });

  test('A-04: unknown locale segment redirects away from bare /fr', async ({ page }) => {
    // Per middleware.ts: unknown first segment → redirect to /en<path>
    // /fr → 307 redirect to /en/fr. The /en/fr path doesn't exist in the app
    // so it returns a 404 NOT-FOUND page, but the URL is /en/fr (not /fr).
    // Key requirement: must NOT return 5xx and must redirect (URL changes from /fr).
    const response = await page.goto(`${BASE}/fr`, { waitUntil: 'load' });
    // Must not be a server error
    expect(response?.status()).toBeLessThan(500);
    // The final URL must have been redirected away from the bare /fr path.
    // Middleware prepends /en, so the URL should contain /en
    const finalUrl = page.url();
    expect(finalUrl).toContain('/en');
    // The bare /fr (without /en prefix) must not be the landing page
    expect(finalUrl).not.toMatch(/localhost:\d+\/fr$/);
  });
});

// ── Group B — Language switcher ────────────────────────────────────────────────

test.describe('B. Language switcher', () => {
  test('B-01: switcher is visible in top nav on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const switcher = page.getByTestId('lang-switcher');
    await expect(switcher).toBeVisible();
  });

  test('B-02: switcher is visible in top nav on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const switcher = page.getByTestId('lang-switcher');
    await expect(switcher).toBeVisible();
  });

  test('B-03: clicking AR link navigates to /ar and sets lang="ar" dir="rtl"', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const switcher = page.getByTestId('lang-switcher');
    // Find the ع / AR link inside the switcher
    const arLink = switcher.getByRole('link', { name: /ع/u });
    await arLink.click();
    // The LanguageSwitcher uses plain <a> tags — clicking triggers a full SSR
    // cycle (not a soft client-side nav), so <html lang dir> MUST update.
    await page.waitForURL(/\/ar/, { timeout: 15_000 });
    expect(page.url()).toContain('/ar');
    // After SSR navigation the root layout re-renders with locale="ar"
    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'ar');
    await expect(html).toHaveAttribute('dir', 'rtl');
  });

  test('B-04: clicking EN link navigates to /en and sets lang="en" dir="ltr"', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const switcher = page.getByTestId('lang-switcher');
    const enLink = switcher.getByRole('link', { name: /EN/i });
    await enLink.click();
    // The LanguageSwitcher uses plain <a> tags — clicking triggers a full SSR
    // cycle (not a soft client-side nav), so <html lang dir> MUST update.
    await page.waitForURL(/\/en/, { timeout: 15_000 });
    expect(page.url()).toContain('/en');
    // After SSR navigation the root layout re-renders with locale="en"
    const html = page.locator('html');
    await expect(html).toHaveAttribute('lang', 'en');
    await expect(html).toHaveAttribute('dir', 'ltr');
  });

  test('B-05: active locale segment is marked with aria-current on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const switcher = page.getByTestId('lang-switcher');
    const enLink = switcher.getByRole('link', { name: /EN/i });
    await expect(enLink).toHaveAttribute('aria-current', 'true');
  });

  test('B-06: active locale segment is marked with aria-current on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const switcher = page.getByTestId('lang-switcher');
    const arLink = switcher.getByRole('link', { name: /ع/u });
    await expect(arLink).toHaveAttribute('aria-current', 'true');
  });

  test('B-07: footer العربية stub is absent (removed — replaced by top-nav switcher)', async ({
    page,
  }) => {
    // The old footer stub was a link with text "العربية" in the links list.
    // It must have been removed (SiteFooter now renders only privacy/terms/support).
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const footer = page.locator('footer');
    // The footer should NOT contain a standalone العربية link (the nav footer
    // links are privacy/terms/support). Note: Arabic text appears in the /ar
    // footer rights line, but the stub link itself must not exist in the EN page.
    const arabicStubLink = footer.locator('a[href*="ar"]').filter({ hasText: 'العربية' });
    await expect(arabicStubLink).toHaveCount(0);
  });
});

// ── Group C — BenefitsPanel ────────────────────────────────────────────────────

test.describe('C. BenefitsPanel', () => {
  test('C-01: benefits-panel renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const panel = page.getByTestId('benefits-panel');
    await expect(panel).toBeVisible();
  });

  test('C-02: benefits-panel renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const panel = page.getByTestId('benefits-panel');
    await expect(panel).toBeVisible();
  });

  test('C-03: EN heading text is present', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const panel = page.getByTestId('benefits-panel');
    // The exact EN heading from copy.ts §benefits.heading
    await expect(panel).toContainText('Set up once. Watch them learn forever.');
  });

  test('C-04: AR heading text is present', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const panel = page.getByTestId('benefits-panel');
    // The exact AR heading from copy.ts §benefits.heading
    await expect(panel).toContainText('أعِدّه مرة واحدة. وشاهدهم يتعلمون للأبد.');
  });

  test('C-05: benefits panel is not empty (has list items) on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const panel = page.getByTestId('benefits-panel');
    // 3-row benefit list — each item has an icon tile + text
    const items = panel.locator('[role="list"] li');
    await expect(items).toHaveCount(3);
  });

  test('C-06: benefits panel is not empty (has list items) on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const panel = page.getByTestId('benefits-panel');
    const items = panel.locator('[role="list"] li');
    await expect(items).toHaveCount(3);
  });
});

// ── Group D — ActivityChart ────────────────────────────────────────────────────

test.describe('D. ActivityChart', () => {
  test('D-01: activity-chart renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    await expect(page.getByTestId('activity-chart')).toBeVisible();
  });

  test('D-02: activity-chart renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    await expect(page.getByTestId('activity-chart')).toBeVisible();
  });

  test('D-03: 7 bar columns are rendered', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    // Each bar column has data-testid="activity-chart-col-N"
    const bars = page.getByTestId('activity-chart-bars');
    await expect(bars).toBeVisible();
    // Verify 7 columns by checking cols 0–6 all exist
    for (let i = 0; i < 7; i++) {
      await expect(page.getByTestId(`activity-chart-col-${i}`)).toBeVisible();
    }
  });

  test('D-04: Export CSV button is present but inert — no navigation on click', async ({
    page,
  }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const chart = page.getByTestId('activity-chart');
    // Button is identified by aria-label set to exportBtn copy
    const exportBtn = chart.getByRole('button', { name: /export csv/i });
    await expect(exportBtn).toBeVisible();

    // Click must not navigate away from /en
    const urlBefore = page.url();
    await exportBtn.click();
    // Wait a tick in case any navigation was triggered
    await page.waitForTimeout(500);
    expect(page.url()).toBe(urlBefore);
  });

  test('D-05: Export CSV button on /ar is present and inert', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const chart = page.getByTestId('activity-chart');
    // AR label is 'تصدير CSV'
    const exportBtn = chart.getByRole('button');
    await expect(exportBtn).toBeVisible();

    const urlBefore = page.url();
    await exportBtn.click();
    await page.waitForTimeout(500);
    expect(page.url()).toBe(urlBefore);
  });

  test('D-06: /ar bar labels contain Arabic-Indic numerals', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    // The value labels inside the bars (aria-hidden spans) should show ٤٥ etc.
    // We use the bars track to scope the search.
    const barsTrack = page.getByTestId('activity-chart-bars');
    // Get the full text content of the bars section
    const text = await barsTrack.textContent();
    expect(text).toBeTruthy();
    // At least one Arabic-Indic digit must be present
    expect(AR_INDIC_PATTERN.test(text ?? '')).toBe(true);
  });

  test('D-07: /en bar labels contain Western digits (not Arabic-Indic)', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const barsTrack = page.getByTestId('activity-chart-bars');
    // Find a visible value label — we check the first bar column
    const firstCol = page.getByTestId('activity-chart-col-0');
    const text = await firstCol.textContent();
    expect(text).toBeTruthy();
    // Western digit '4' is the first char of '45' (EN value for Mon)
    expect(text).toContain('45');
    // No Arabic-Indic digits in EN
    expect(AR_INDIC_PATTERN.test(text ?? '')).toBe(false);
  });
});

// ── Group E — AITutorBubble ────────────────────────────────────────────────────

test.describe('E. AITutorBubble', () => {
  test('E-01: ai-tutor-bubble renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    await expect(page.getByTestId('ai-tutor-bubble')).toBeVisible();
  });

  test('E-02: ai-tutor-bubble renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    await expect(page.getByTestId('ai-tutor-bubble')).toBeVisible();
  });

  test('E-03: mascot image loads without error (no broken img) on /en', async ({ page }) => {
    // Collect any failed image requests
    const failedImages: string[] = [];
    page.on('requestfailed', (req) => {
      if (req.resourceType() === 'image') failedImages.push(req.url());
    });
    await page.goto(`${BASE}/en`, { waitUntil: 'networkidle' });

    // Check that the mascot-owl.svg image is present
    const mascot = page.locator('img[src*="mascot-owl"]');
    await expect(mascot).toBeVisible();

    // Verify the image loaded successfully (naturalWidth > 0 means the browser
    // decoded it; SVGs may report 0 for naturalWidth, so we check for not broken)
    const isBroken = await mascot.evaluate(
      (img: HTMLImageElement) => img.complete && img.naturalWidth === 0,
    );
    expect(isBroken).toBe(false);

    // No failed image requests during load
    const owlFailures = failedImages.filter((url) => url.includes('mascot-owl'));
    expect(owlFailures).toHaveLength(0);
  });

  test('E-04: EN chip text is present (3 chips)', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const chips = page.getByTestId('ai-tutor-chips');
    await expect(chips).toBeVisible();
    // Chips from copy.en.tutorBubble.chips
    await expect(chips).toContainText('Yes, show me');
    await expect(chips).toContainText('Give a hint');
    await expect(chips).toContainText('Skip');
  });

  test('E-05: AR chip text is present (3 chips)', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const chips = page.getByTestId('ai-tutor-chips');
    await expect(chips).toBeVisible();
    // Chips from copy.ar.tutorBubble.chips
    await expect(chips).toContainText('نعم، أرني');
    await expect(chips).toContainText('أعطني تلميحاً');
    await expect(chips).toContainText('تخطي');
  });

  test('E-06: bubble tail uses logical property — data-dir reflects locale on /en', async ({
    page,
  }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    // The .stage element gets data-dir="ltr" in EN (set in AITutorBubble.tsx)
    const stage = page
      .getByTestId('ai-tutor-bubble')
      .locator('[data-dir]')
      .first();
    await expect(stage).toHaveAttribute('data-dir', 'ltr');
  });

  test('E-07: bubble tail uses logical property — data-dir reflects locale on /ar', async ({
    page,
  }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const stage = page
      .getByTestId('ai-tutor-bubble')
      .locator('[data-dir]')
      .first();
    await expect(stage).toHaveAttribute('data-dir', 'rtl');
  });

  test('E-08: bubble border-end-start-radius resolves to bottom-left in LTR (EN)', async ({
    page,
  }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const bubble = page.getByTestId('ai-tutor-bubble-inner');
    // In LTR border-end-start-radius = bottom-left = borderBottomLeftRadius
    // The spec says it should be 4px; the other corners should be 22px.
    const computed = await bubble.evaluate((el: Element) => {
      const style = window.getComputedStyle(el);
      return {
        bottomLeft: style.borderBottomLeftRadius,
        bottomRight: style.borderBottomRightRadius,
      };
    });
    // bottom-left (end-start in LTR) should be the small tail corner (4px)
    expect(computed.bottomLeft).toBe('4px');
    // bottom-right (end-end in LTR) should be the full radius (~22px)
    expect(computed.bottomRight).toBe('22px');
  });

  test('E-09: bubble border-end-start-radius resolves to bottom-right in RTL (AR)', async ({
    page,
  }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const bubble = page.getByTestId('ai-tutor-bubble-inner');
    // In RTL border-end-start-radius = bottom-right = borderBottomRightRadius
    const computed = await bubble.evaluate((el: Element) => {
      const style = window.getComputedStyle(el);
      return {
        bottomLeft: style.borderBottomLeftRadius,
        bottomRight: style.borderBottomRightRadius,
      };
    });
    // bottom-right (end-start in RTL) should be the tail corner (4px)
    expect(computed.bottomRight).toBe('4px');
    // bottom-left (end-end in RTL) should be the full radius (~22px)
    expect(computed.bottomLeft).toBe('22px');
  });
});

// ── Group F — ChildCardPhone ───────────────────────────────────────────────────

test.describe('F. ChildCardPhone', () => {
  test('F-01: child-card-phone renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    await expect(page.getByTestId('child-card-phone')).toBeVisible();
  });

  test('F-02: child-card-phone renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    await expect(page.getByTestId('child-card-phone')).toBeVisible();
  });

  test('F-03: email element has direction: ltr in EN locale', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const email = page.getByTestId('child-card-email');
    await expect(email).toBeVisible();
    const direction = await email.evaluate(
      (el: Element) => window.getComputedStyle(el).direction,
    );
    expect(direction).toBe('ltr');
  });

  test('F-04: email element has direction: ltr even in AR locale (pinned)', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const email = page.getByTestId('child-card-email');
    await expect(email).toBeVisible();
    const direction = await email.evaluate(
      (el: Element) => window.getComputedStyle(el).direction,
    );
    // The CSS applies direction:ltr to .em in BOTH locales (technical string)
    expect(direction).toBe('ltr');
  });

  test('F-05: email shows the expected address', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const email = page.getByTestId('child-card-email');
    await expect(email).toHaveText('sami@learnexia.com');
  });

  test('F-06: footer contains › (right chevron) in EN', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const footer = page.getByTestId('child-card-footer');
    await expect(footer).toBeVisible();
    // EN view-progress: "View progress →"; chevron row1: "›"
    const row1 = page.getByTestId('child-card-row1');
    const text = await row1.textContent();
    expect(text).toContain('›');
  });

  test('F-07: footer contains ‹ (left chevron) in AR', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const row1 = page.getByTestId('child-card-row1');
    const text = await row1.textContent();
    expect(text).toContain('‹');
  });

  test('F-08: footer arrow is → in EN (View progress →)', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const footer = page.getByTestId('child-card-footer');
    const text = await footer.textContent();
    expect(text).toContain('View progress →');
  });

  test('F-09: footer arrow is ← in AR (عرض التقدم ←)', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const footer = page.getByTestId('child-card-footer');
    const text = await footer.textContent();
    expect(text).toContain('عرض التقدم ←');
  });

  test('F-10: stats row has Arabic-Indic numerals on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const stats = page.getByTestId('child-card-stats');
    const text = await stats.textContent();
    // AR stats: المستوى ١٢ / ١٬٢٤٠ / ٧ أيام — all contain Arabic-Indic digits
    expect(AR_INDIC_PATTERN.test(text ?? '')).toBe(true);
  });
});

// ── Group G — RTL layout at mobile width ─────────────────────────────────────

test.describe('G. RTL layout at mobile width (390px)', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('G-01: /en renders all 4 components without overflow at 390px', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    for (const testId of [
      'ai-tutor-bubble',
      'child-card-phone',
      'activity-chart',
      'benefits-panel',
    ]) {
      const el = page.getByTestId(testId);
      await expect(el).toBeVisible();
      // Check the element doesn't overflow the viewport
      const box = await el.boundingBox();
      expect(box).not.toBeNull();
      if (box) {
        // Nothing should start before 0 (x) or be wider than viewport
        expect(box.x).toBeGreaterThanOrEqual(-1); // allow 1px rounding
        expect(box.width).toBeLessThanOrEqual(395); // 390 + 5px tolerance
      }
    }
  });

  test('G-02: /ar renders all 4 components without overflow at 390px (RTL)', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    for (const testId of [
      'ai-tutor-bubble',
      'child-card-phone',
      'activity-chart',
      'benefits-panel',
    ]) {
      const el = page.getByTestId(testId);
      await expect(el).toBeVisible();
      const box = await el.boundingBox();
      expect(box).not.toBeNull();
      if (box) {
        expect(box.x).toBeGreaterThanOrEqual(-1);
        expect(box.width).toBeLessThanOrEqual(395);
      }
    }
  });

  test('G-03: ActivityChart bars row-reverse in RTL at mobile width', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const barsTrack = page.getByTestId('activity-chart-bars');
    const flexDir = await barsTrack.evaluate(
      (el: Element) => window.getComputedStyle(el).flexDirection,
    );
    expect(flexDir).toBe('row-reverse');
  });

  test('G-04: ActivityChart bars are LTR in EN at mobile width', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const barsTrack = page.getByTestId('activity-chart-bars');
    const flexDir = await barsTrack.evaluate(
      (el: Element) => window.getComputedStyle(el).flexDirection,
    );
    // Default row (not row-reverse) in LTR
    expect(flexDir).toBe('row');
  });
});

// ── Group H — No console errors ───────────────────────────────────────────────

test.describe('H. No console errors', () => {
  test('H-01: zero console errors on /en page load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    page.on('pageerror', (err) => errors.push(err.message));

    await page.goto(`${BASE}/en`, { waitUntil: 'networkidle' });

    // Filter out known benign browser noise (e.g. favicon missing in dev mode)
    const realErrors = errors.filter(
      (e) =>
        !e.includes('favicon') &&
        !e.includes('ERR_ABORTED') &&
        !e.includes('Failed to fetch') &&
        // Next.js dev mode HMR noise
        !e.includes('Fast refresh') &&
        !e.includes('webpack'),
    );
    expect(realErrors, `Console errors on /en: ${realErrors.join('\n')}`).toHaveLength(0);
  });

  test('H-02: zero console errors on /ar page load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    page.on('pageerror', (err) => errors.push(err.message));

    await page.goto(`${BASE}/ar`, { waitUntil: 'networkidle' });

    const realErrors = errors.filter(
      (e) =>
        !e.includes('favicon') &&
        !e.includes('ERR_ABORTED') &&
        !e.includes('Failed to fetch') &&
        !e.includes('Fast refresh') &&
        !e.includes('webpack'),
    );
    expect(realErrors, `Console errors on /ar: ${realErrors.join('\n')}`).toHaveLength(0);
  });
});
