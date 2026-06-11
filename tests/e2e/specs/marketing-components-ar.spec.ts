/**
 * E2E spec — Marketing site: ParentValueSection + Arabic/RTL
 *
 * Story: marketing-components-ar (ad-hoc enhancement)
 * Plan:  docs/plans/marketing-components-ar.md (E2E-01, E2E-02)
 * Brief: docs/briefs/marketing-components-ar.md
 * Design spec: design-system/ui_kits/marketing/for-parents-section.md
 *
 * Target: Next.js 15 marketing site on http://localhost:3002
 * Project: "marketing" / "marketing-mobile" (see playwright.config.ts)
 *
 * Groups:
 *   A. Locale routing                (unchanged)
 *   B. Language switcher             (unchanged)
 *   C. ParentValueSection — section + header
 *   D. ParentValueSection — Benefits panel
 *   E. ParentValueSection — Activity chart
 *   F. ParentValueSection — AI tutor bubble
 *   G. ParentValueSection — Child card
 *   H. RTL layout at mobile width    (updated to new testids)
 *   I. No console errors             (unchanged, was H)
 *
 * NOTE: The four standalone components (BenefitsPanel, ActivityChart,
 * AITutorBubble, ChildCardPhone) were removed from the page and replaced by
 * the composed ParentValueSection. Groups C–G cover the new component;
 * legacy testids are asserted ABSENT in C-05.
 */

import { test, expect } from '@playwright/test';

// ── constants ──────────────────────────────────────────────────────────────────

/** Base URL for the marketing site; the project config already sets this but
 *  we import it here to make navigations explicit and portable when run outside
 *  a project context (e.g. `--project=chromium` by mistake). */
const BASE = process.env.MARKETING_URL ?? 'http://localhost:3002';

// Arabic-Indic digits (U+0660–U+0669)
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

// ── Group C — ParentValueSection: section wrapper + header ────────────────────

test.describe('C. ParentValueSection — section + header', () => {
  test('C-01: parent-value-section renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    // The section uses IntersectionObserver to reveal; scroll to it first.
    const section = page.getByTestId('parent-value-section');
    await section.scrollIntoViewIfNeeded();
    await expect(section).toBeVisible();
  });

  test('C-02: parent-value-section renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const section = page.getByTestId('parent-value-section');
    await section.scrollIntoViewIfNeeded();
    await expect(section).toBeVisible();
  });

  test('C-03: EN eyebrow "For Parents" is present', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const section = page.getByTestId('parent-value-section');
    await section.scrollIntoViewIfNeeded();
    await expect(section).toContainText('For Parents');
  });

  test('C-04: EN heading is present', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const section = page.getByTestId('parent-value-section');
    await section.scrollIntoViewIfNeeded();
    // EN heading from copy.ts parentValue.heading
    await expect(section).toContainText('See exactly what your child gets out of it.');
  });

  test('C-05: AR eyebrow "لأولياء الأمور" is present', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const section = page.getByTestId('parent-value-section');
    await section.scrollIntoViewIfNeeded();
    await expect(section).toContainText('لأولياء الأمور');
  });

  test('C-06: AR heading is present', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const section = page.getByTestId('parent-value-section');
    await section.scrollIntoViewIfNeeded();
    // AR heading from copy.ts ar.parentValue.heading
    await expect(section).toContainText('شاهد بالضبط ما يستفيده طفلك.');
  });

  test('C-07: deleted testids are absent — old standalone bands are gone', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    // Sanity: the four removed standalone components must not appear anywhere on the page.
    await expect(page.getByTestId('benefits-panel')).toHaveCount(0);
    await expect(page.getByTestId('activity-chart')).toHaveCount(0);
    await expect(page.getByTestId('ai-tutor-bubble')).toHaveCount(0);
    await expect(page.getByTestId('child-card-phone')).toHaveCount(0);
  });
});

// ── Group D — ParentValueSection: Benefits panel ──────────────────────────────

test.describe('D. ParentValueSection — Benefits panel', () => {
  test('D-01: parent-value-panel renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const panel = page.getByTestId('parent-value-panel');
    await panel.scrollIntoViewIfNeeded();
    await expect(panel).toBeVisible();
  });

  test('D-02: parent-value-panel renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const panel = page.getByTestId('parent-value-panel');
    await panel.scrollIntoViewIfNeeded();
    await expect(panel).toBeVisible();
  });

  test('D-03: EN panel heading is present', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const panel = page.getByTestId('parent-value-panel');
    await panel.scrollIntoViewIfNeeded();
    // EN heading from copy.ts parentValue.panel.heading
    await expect(panel).toContainText('Set up once. Watch them learn forever.');
  });

  test('D-04: AR panel heading is present', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const panel = page.getByTestId('parent-value-panel');
    await panel.scrollIntoViewIfNeeded();
    // AR heading from copy.ts ar.parentValue.panel.heading
    await expect(panel).toContainText('جهّز الحساب مرة. شاهدهم يتعلمون للأبد.');
  });

  test('D-05: EN panel has exactly 4 bullet rows', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const panel = page.getByTestId('parent-value-panel');
    await panel.scrollIntoViewIfNeeded();
    // 4 bullets per spec §LEFT — ✨ 📊 🎯 🛡️
    const items = panel.locator('[role="list"] li');
    await expect(items).toHaveCount(4);
  });

  test('D-06: AR panel has exactly 4 bullet rows', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const panel = page.getByTestId('parent-value-panel');
    await panel.scrollIntoViewIfNeeded();
    const items = panel.locator('[role="list"] li');
    await expect(items).toHaveCount(4);
  });
});

// ── Group E — ParentValueSection: Activity chart ──────────────────────────────

test.describe('E. ParentValueSection — Activity chart', () => {
  test('E-01: parent-value-chart renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const chart = page.getByTestId('parent-value-chart');
    await chart.scrollIntoViewIfNeeded();
    await expect(chart).toBeVisible();
  });

  test('E-02: parent-value-chart renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const chart = page.getByTestId('parent-value-chart');
    await chart.scrollIntoViewIfNeeded();
    await expect(chart).toBeVisible();
  });

  test('E-03: chart title "Your weekly report" is present on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const chart = page.getByTestId('parent-value-chart');
    await chart.scrollIntoViewIfNeeded();
    await expect(chart).toContainText('Your weekly report');
  });

  test('E-04: chart title is present in AR on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const chart = page.getByTestId('parent-value-chart');
    await chart.scrollIntoViewIfNeeded();
    await expect(chart).toContainText('تقريرك الأسبوعي');
  });

  test('E-05: delta "+28%" text is present on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const chart = page.getByTestId('parent-value-chart');
    await chart.scrollIntoViewIfNeeded();
    // EN delta: "+28% vs last week"
    await expect(chart).toContainText('+28%');
  });

  test('E-06: delta "↑28%" / Arabic variant text is present on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const chart = page.getByTestId('parent-value-chart');
    await chart.scrollIntoViewIfNeeded();
    // AR delta from copy.ts: '+٢٨٪ عن الأسبوع الماضي' — contains Arabic-Indic + ٪
    const text = await chart.textContent();
    // Must contain Arabic-Indic numeral(s) in the delta
    expect(AR_INDIC_PATTERN.test(text ?? '')).toBe(true);
  });

  test('E-07: 7 bar columns are rendered in parent-value-chart-bars on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const barsRow = page.getByTestId('parent-value-chart-bars');
    await barsRow.scrollIntoViewIfNeeded();
    await expect(barsRow).toBeVisible();
    // 7 direct children (one barCol per day Mon–Sun)
    const barCols = barsRow.locator(':scope > div');
    await expect(barCols).toHaveCount(7);
  });

  test('E-08: 7 bar columns are rendered in parent-value-chart-bars on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const barsRow = page.getByTestId('parent-value-chart-bars');
    await barsRow.scrollIntoViewIfNeeded();
    const barCols = barsRow.locator(':scope > div');
    await expect(barCols).toHaveCount(7);
  });

  test('E-09: Sunday (last) bar has highlighted gradient class on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const barsRow = page.getByTestId('parent-value-chart-bars');
    await barsRow.scrollIntoViewIfNeeded();
    // The 7th column (index 6) contains the highlighted bar with barHi CSS module class.
    // The class name is mangled by Next.js CSS modules but the bar should have a
    // distinct background from the Sunday gradient (--lx-grad-levelup-180).
    // We verify the Sunday bar exists and is the tallest (height = 95px per spec).
    const barCols = barsRow.locator(':scope > div');
    const sundayCol = barCols.nth(6);
    const bar = sundayCol.locator(':scope > div').first();
    const height = await bar.evaluate((el: Element) => (el as HTMLElement).style.height);
    // Spec: Sunday XP=110 → (110/110)*95 = 95px
    expect(height).toBe('95px');
  });
});

// ── Group F — ParentValueSection: AI tutor bubble ─────────────────────────────

test.describe('F. ParentValueSection — AI tutor bubble', () => {
  test('F-01: parent-value-tutor renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const tutor = page.getByTestId('parent-value-tutor');
    await tutor.scrollIntoViewIfNeeded();
    await expect(tutor).toBeVisible();
  });

  test('F-02: parent-value-tutor renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const tutor = page.getByTestId('parent-value-tutor');
    await tutor.scrollIntoViewIfNeeded();
    await expect(tutor).toBeVisible();
  });

  test('F-03: parent-value-tutor-bubble renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    await expect(bubble).toBeVisible();
  });

  test('F-04: EN tutor label "Lexi · AI Tutor" is present', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    // From copy.ts en.parentValue.tutor.label
    await expect(bubble).toContainText('Lexi · AI Tutor');
  });

  test('F-05: AR tutor label "ليكسي · المعلم الذكي" is present', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    // From copy.ts ar.parentValue.tutor.label
    await expect(bubble).toContainText('ليكسي · المعلم الذكي');
  });

  test('F-06: EN bubble message is present', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    // Check that the message fragment is rendered (lead text)
    await expect(bubble).toContainText('When we compare two numbers');
  });

  test('F-07: AR bubble message is present', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    await expect(bubble).toContainText('عندما نقارن عددين');
  });

  test('F-08: bubble tail corner — border-end-start-radius is 4px in LTR/EN', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    // In LTR: border-end-start-radius maps to borderBottomLeftRadius.
    // Spec §(2): bubble has border-end-start-radius:4px (EN, avatar on leading/left).
    const computed = await bubble.evaluate((el: Element) => {
      const s = window.getComputedStyle(el);
      return {
        bottomLeft: s.borderBottomLeftRadius,
        bottomRight: s.borderBottomRightRadius,
      };
    });
    expect(computed.bottomLeft).toBe('4px');
    expect(computed.bottomRight).toBe('22px');
  });

  test('F-09: bubble tail corner flips — border-end-end-radius is 4px in RTL/AR', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const bubble = page.getByTestId('parent-value-tutor-bubble');
    await bubble.scrollIntoViewIfNeeded();
    // In RTL: border-end-end-radius maps to borderBottomRightRadius.
    // Spec §4.3 + index-ar.html line 170: border-bottom-right-radius:4px in AR.
    // The CSS uses [dir="rtl"] .tutorBubble { border-end-start-radius:22px; border-end-end-radius:4px }
    const computed = await bubble.evaluate((el: Element) => {
      const s = window.getComputedStyle(el);
      return {
        bottomLeft: s.borderBottomLeftRadius,
        bottomRight: s.borderBottomRightRadius,
      };
    });
    expect(computed.bottomRight).toBe('4px');
    expect(computed.bottomLeft).toBe('22px');
  });

  test('F-10: no suggestion chips are rendered (spec: chips omitted in composed section)', async ({
    page,
  }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const tutor = page.getByTestId('parent-value-tutor');
    await tutor.scrollIntoViewIfNeeded();
    // The composed section must NOT render suggestion chips.
    // The old standalone AITutorBubble had testid "ai-tutor-chips" — that element is absent.
    await expect(page.getByTestId('ai-tutor-chips')).toHaveCount(0);
  });
});

// ── Group G — ParentValueSection: Child card ──────────────────────────────────

test.describe('G. ParentValueSection — Child card', () => {
  test('G-01: parent-value-child-card renders on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const card = page.getByTestId('parent-value-child-card');
    await card.scrollIntoViewIfNeeded();
    await expect(card).toBeVisible();
  });

  test('G-02: parent-value-child-card renders on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const card = page.getByTestId('parent-value-child-card');
    await card.scrollIntoViewIfNeeded();
    await expect(card).toBeVisible();
  });

  test('G-03: email is "sami@learnexia.com" on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const email = page.getByTestId('parent-value-child-email');
    await email.scrollIntoViewIfNeeded();
    await expect(email).toHaveText('sami@learnexia.com');
  });

  test('G-04: email is "sami@learnexia.com" on /ar (LTR-pinned, unchanged)', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const email = page.getByTestId('parent-value-child-email');
    await email.scrollIntoViewIfNeeded();
    // Email address never localises per spec §4.5
    await expect(email).toHaveText('sami@learnexia.com');
  });

  test('G-05: email element has direction:ltr on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const email = page.getByTestId('parent-value-child-email');
    await email.scrollIntoViewIfNeeded();
    const dir = await email.evaluate((el: Element) => window.getComputedStyle(el).direction);
    expect(dir).toBe('ltr');
  });

  test('G-06: email element has direction:ltr even on /ar (pinned per spec §4.5)', async ({
    page,
  }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const email = page.getByTestId('parent-value-child-email');
    await email.scrollIntoViewIfNeeded();
    const dir = await email.evaluate((el: Element) => window.getComputedStyle(el).direction);
    // The CSS class always sets direction:ltr (technical string) per spec §4.5
    expect(dir).toBe('ltr');
  });

  test('G-07: "View progress →" CTA is present on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const cta = page.getByTestId('parent-value-child-cta');
    await cta.scrollIntoViewIfNeeded();
    await expect(cta).toBeVisible();
    await expect(cta).toContainText('View progress');
  });

  test('G-08: "عرض التقدم ←" CTA is present on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const cta = page.getByTestId('parent-value-child-cta');
    await cta.scrollIntoViewIfNeeded();
    await expect(cta).toBeVisible();
    await expect(cta).toContainText('عرض التقدم ←');
  });

  test('G-09: stats row has Western numerals on /en (1,240 / Lv 12 / 7d)', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const stats = page.getByTestId('parent-value-child-stats');
    await stats.scrollIntoViewIfNeeded();
    const text = await stats.textContent();
    // EN copy: "🧠 Lv 12", "⭐ 1,240", "🔥 7d"
    expect(text).toContain('1,240');
    expect(text).toContain('12');
    expect(text).toContain('7');
    // No Arabic-Indic numerals in the EN stats row
    expect(AR_INDIC_PATTERN.test(text ?? '')).toBe(false);
  });

  test('G-10: stats row has Arabic-Indic numerals on /ar (١٬٢٤٠ / ١٢ / ٧)', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const stats = page.getByTestId('parent-value-child-stats');
    await stats.scrollIntoViewIfNeeded();
    const text = await stats.textContent();
    // AR copy: "🧠 المستوى ١٢", "⭐ ١٬٢٤٠", "🔥 ٧ أيام"
    expect(AR_INDIC_PATTERN.test(text ?? '')).toBe(true);
    // Specific expected AR-Indic values
    expect(text).toContain('١٬٢٤٠');
    expect(text).toContain('١٢');
    expect(text).toContain('٧');
  });

  test('G-11: parent-value-child-status (Active today) is present on /en', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const status = page.getByTestId('parent-value-child-status');
    await status.scrollIntoViewIfNeeded();
    await expect(status).toBeVisible();
    await expect(status).toContainText('Active today');
  });

  test('G-12: parent-value-child-status is present in AR on /ar', async ({ page }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const status = page.getByTestId('parent-value-child-status');
    await status.scrollIntoViewIfNeeded();
    await expect(status).toContainText('نشط اليوم');
  });
});

// ── Group H — RTL layout at mobile width (390px) ─────────────────────────────
// Updated from old Group G: now references parent-value-* testids per the new
// composed ParentValueSection (old standalone component testids removed from page).

test.describe('H. RTL layout at mobile width (390px)', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('H-01: /en — parent-value-section + key sub-elements render without overflow at 390px', async ({
    page,
  }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    for (const testId of [
      'parent-value-section',
      'parent-value-panel',
      'parent-value-chart',
      'parent-value-tutor',
      'parent-value-child-card',
    ]) {
      const el = page.getByTestId(testId);
      await el.scrollIntoViewIfNeeded();
      await expect(el).toBeVisible();
      const box = await el.boundingBox();
      expect(box).not.toBeNull();
      if (box) {
        // Nothing should start before 0 (x) or be wider than viewport (390px + 5px tolerance)
        expect(box.x).toBeGreaterThanOrEqual(-1);
        expect(box.width).toBeLessThanOrEqual(395);
      }
    }
  });

  test('H-02: /ar — parent-value-section + key sub-elements render without overflow at 390px (RTL)', async ({
    page,
  }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    for (const testId of [
      'parent-value-section',
      'parent-value-panel',
      'parent-value-chart',
      'parent-value-tutor',
      'parent-value-child-card',
    ]) {
      const el = page.getByTestId(testId);
      await el.scrollIntoViewIfNeeded();
      await expect(el).toBeVisible();
      const box = await el.boundingBox();
      expect(box).not.toBeNull();
      if (box) {
        expect(box.x).toBeGreaterThanOrEqual(-1);
        expect(box.width).toBeLessThanOrEqual(395);
      }
    }
  });

  test('H-03: chart bars row (parent-value-chart-bars) has direction:ltr in RTL/AR — bars never reverse', async ({
    page,
  }) => {
    await page.goto(`${BASE}/ar`, { waitUntil: 'load' });
    const barsRow = page.getByTestId('parent-value-chart-bars');
    await barsRow.scrollIntoViewIfNeeded();
    // Spec §4.2: AR source wraps bars in direction:ltr so Mon→Sun always reads L→R.
    const dir = await barsRow.evaluate(
      (el: Element) => window.getComputedStyle(el).direction,
    );
    expect(dir).toBe('ltr');
  });

  test('H-04: chart bars row has direction:ltr in LTR/EN as well', async ({ page }) => {
    await page.goto(`${BASE}/en`, { waitUntil: 'load' });
    const barsRow = page.getByTestId('parent-value-chart-bars');
    await barsRow.scrollIntoViewIfNeeded();
    const dir = await barsRow.evaluate(
      (el: Element) => window.getComputedStyle(el).direction,
    );
    expect(dir).toBe('ltr');
  });
});

// ── Group I — No console errors ───────────────────────────────────────────────

test.describe('I. No console errors', () => {
  test('I-01: zero console errors on /en page load', async ({ page }) => {
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

  test('I-02: zero console errors on /ar page load', async ({ page }) => {
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
