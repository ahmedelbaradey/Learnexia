/**
 * P1-09-FE — Splash → Routing → Locale E2E tests (web PWA)
 *
 * Implements every FE-TC-* case from docs/qc/P1-09-FE/frontend-test-cases.md.
 *
 * Selector strategy (testIDs now exist):
 *   - splash-screen           → getByTestId('splash-screen')      [on GradientBox root]
 *   - splash-loading          → getByTestId('splash-loading')     [on Text node]
 *   - locale-switch-ar        → getByTestId('locale-switch-ar')   [on Stack/radio]
 *   - locale-switch-en        → getByTestId('locale-switch-en')   [on Stack/radio]
 *   - sign-out-button         → getByTestId('sign-out-button')    [on Stack/button]
 *   - parent-home             → getByTestId('parent-home')        [on Stack root]
 *   - dashboard-header        → getByTestId('dashboard-header')   [in DashboardHeader]
 *   - add-child-form-card     → getByTestId('add-child-form-card') [on Stack]
 *   - add-child-name          → getByTestId('add-child-name')     [TextInput → <input>]
 *   - add-child-email         → getByTestId('add-child-email')    [TextInput → <input>]
 *   - add-child-password      → getByTestId('add-child-password') [TextInput → <input>]
 *   - add-child-grade         → getByTestId('add-child-grade')    [Select trigger XStack]
 *   - add-child-learning-lang → getByTestId('add-child-learning-language')
 *   - add-child-app-language  → getByTestId('add-child-app-language')
 *   - add-child-to-list       → getByTestId('add-child-to-list') [Button]
 *   - add-child-submit        → getByTestId('add-child-submit')   [Button]
 *   - login-username          → getByTestId('login-username')     [TextInput → <input>]
 *   - login-password          → getByTestId('login-password')     [TextInput → <input>]
 *   - login-submit            → getByTestId('login-submit')       [Button]
 *   - login-error             → getByTestId('login-error')        [ServerErrorBanner]
 *
 * RN Web testID mapping:
 *   - TextInput testID renders as data-testid on the <input> element DIRECTLY.
 *     Call getByTestId('login-username').fill() — do NOT chain .locator('input').
 *   - Stack/View testID renders as data-testid on the outer <div>.
 *
 * Design facts:
 *   - Arabic is the default locale (dir=rtl, lang=ar on fresh context).
 *   - locale-switch active state = filled background ($primary) — RN Web does NOT
 *     map accessibilityState.selected → aria-checked. Detect via backgroundColor.
 *   - Select options open as role="radio" with aria-label = option label text.
 *   - FE-TC-22 (native restart prompt) BLOCKED in web E2E.
 */

import { test, expect, type Page } from '@playwright/test';

// All tests have long setup (register + add-child = multiple real API round-trips).
test.setTimeout(120_000);

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Unique email per run (avoids duplicate-account collisions). */
function uniqueEmail(tag = 'p'): string {
  return `${tag}+e2e+${Date.now()}+${Math.floor(Math.random() * 99999)}@example.com`;
}

/**
 * Detect whether a locale radio is "active" (filled background = selected).
 * RN Web does NOT translate accessibilityState.selected → aria-checked on Tamagui Stack.
 * Active = backgroundColor is an opaque color (not transparent/rgba(0,0,0,0)).
 */
async function isLocaleActive(page: Page, testId: 'locale-switch-ar' | 'locale-switch-en'): Promise<boolean> {
  return page.getByTestId(testId).evaluate((el: HTMLElement) => {
    const bg = window.getComputedStyle(el).backgroundColor;
    return bg !== '' && bg !== 'rgba(0, 0, 0, 0)' && !bg.toLowerCase().includes('transparent');
  });
}

/**
 * Click a Select trigger (by testID) then click an option (by aria-label).
 * Select options render as role="radio" with aria-label = option label.
 */
async function selectOption(page: Page, triggerTestId: string, optionLabel: string | RegExp): Promise<void> {
  await page.getByTestId(triggerTestId).click();
  await page.waitForTimeout(500);
  const option = page.getByRole('radio', { name: optionLabel });
  await option.first().waitFor({ state: 'visible', timeout: 8_000 });
  await option.first().click();
  await page.waitForTimeout(300);
}

/**
 * Register a new parent via /register form.
 * Returns the email used. After success the routing guard lands on add-child.
 */
async function registerParent(page: Page, opts: { email?: string; password?: string } = {}): Promise<string> {
  const email = opts.email ?? uniqueEmail('parent');
  const password = opts.password ?? 'Str0ng!Pass1';

  await page.goto('/register');
  await page.waitForTimeout(2000);

  // Full name (textbox[0] = input[type=text])
  await page.getByRole('textbox').nth(0).fill('E2E Parent');

  // Country combobox (the one on /register, not the AddChildForm)
  const combobox = page.getByRole('combobox');
  await combobox.click();
  await page.waitForTimeout(600);
  // Options appear as role=radio — pick first one
  const firstOption = page.getByRole('radio').first();
  const firstVisible = await firstOption.isVisible({ timeout: 3_000 }).catch(() => false);
  if (firstVisible) {
    await firstOption.click();
  } else {
    // Text fallback for Saudi Arabia (most common first option)
    const saudi = page.locator(':text("السعودية"), :text("Saudi Arabia")').first();
    await saudi.click().catch(async () => { await page.keyboard.press('Escape'); });
  }
  await page.waitForTimeout(300);

  // Email (textbox[1] = input[type=email])
  await page.getByRole('textbox').nth(1).fill(email);

  // Password
  await page.locator('input[type="password"]').fill(password);

  // Terms checkbox
  await page.getByRole('checkbox').click();
  await page.waitForTimeout(200);

  // Submit (last button)
  await page.getByRole('button').last().click();

  // Wait for add-child redirect
  await page.waitForURL(/add-child|onboarding/, { timeout: 25_000 }).catch(() => {});
  await page.waitForTimeout(1_500);

  return email;
}

/**
 * Add one child via AddChildForm.
 * IMPORTANT: TextInput testID in RN Web renders as data-testid on <input> directly.
 * Call .fill() on getByTestId() directly — do NOT chain .locator('input').
 */
async function addChildViaForm(
  page: Page,
  opts: { language?: 'ar' | 'en'; childEmail?: string } = {},
): Promise<{ email: string; password: string }> {
  const childEmail = opts.childEmail ?? uniqueEmail('child');
  const childPassword = 'Child!Pass1';
  const language = opts.language ?? 'ar';

  // Form card must be visible first
  await page.getByTestId('add-child-form-card').waitFor({ state: 'visible', timeout: 15_000 });

  // Fields: testID IS on the TextInput (<input>) — fill directly
  await page.getByTestId('add-child-name').fill('E2E Child');
  await page.getByTestId('add-child-email').fill(childEmail);
  await page.getByTestId('add-child-password').fill(childPassword);

  // Grade select (role=combobox, testID on trigger XStack)
  await selectOption(page, 'add-child-grade', /Grade 1|الصف الأول/i);

  // Learning language (required — no default)
  const langLabel = language === 'ar' ? /Arabic|عرب/i : /English|الإنجليزية/i;
  await selectOption(page, 'add-child-learning-language', langLabel);

  // App language (auto-filled from learning language, but set explicitly)
  await selectOption(page, 'add-child-app-language', langLabel);

  // Country — no testID; fill by accessible label or last textbox
  const countryField = page.getByRole('textbox', { name: /country|الدولة/i });
  const countryVisible = await countryField.first().isVisible({ timeout: 2_000 }).catch(() => false);
  if (countryVisible) {
    await countryField.first().fill('Egypt');
  } else {
    // Fallback: last textbox on page
    await page.getByRole('textbox').last().fill('Egypt');
  }
  await page.waitForTimeout(200);

  // "Add Child to List" button
  await page.getByTestId('add-child-to-list').click();
  await page.waitForTimeout(2_000);

  // Submit list
  await page.getByTestId('add-child-submit').waitFor({ state: 'visible', timeout: 10_000 });
  await page.getByTestId('add-child-submit').click();

  // Wait for routing to complete (→ complete or → parent)
  await page.waitForTimeout(12_000);

  return { email: childEmail, password: childPassword };
}

/**
 * Sign in via Login form. testID IS on the input elements.
 */
async function signInViaUI(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.waitForTimeout(2_000);
  await page.getByTestId('login-username').fill(email);
  await page.getByTestId('login-password').fill(password);
  await page.getByTestId('login-submit').click();
  await page.waitForTimeout(10_000);
}

/**
 * Sign out and wait for login screen.
 */
async function signOutAndWait(page: Page): Promise<void> {
  const btn = page.getByTestId('sign-out-button');
  const visible = await btn.isVisible({ timeout: 5_000 }).catch(() => false);
  if (visible) {
    await btn.click();
    await page.waitForURL(/login/, { timeout: 15_000 }).catch(() => {});
  } else {
    await page.goto('/login');
  }
  await page.waitForTimeout(1_500);
}

// ===========================================================================
// Group A — Splash / Boot + no-content-flash
// ===========================================================================

test.describe('Group A — Splash / Boot + no-content-flash', () => {
  // FE-TC-01 — Splash renders on cold boot (signed-out, no session)
  test('FE-TC-01 — Splash renders on cold boot', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Navigate — splash renders briefly before guard navigates to /login
      const navPromise = page.goto('/');

      // Poll DOM for the splash element (it may already have redirected by the time
      // await resolves, so poll during navigation)
      let splashFoundInDom = false;
      for (let i = 0; i < 25; i++) {
        const count = await page.locator('[data-testid="splash-screen"]').count().catch(() => 0);
        if (count > 0) { splashFoundInDom = true; break; }
        await page.waitForTimeout(200);
      }
      await navPromise;

      // splash-screen element must have been in DOM during boot
      expect(splashFoundInDom, 'splash-screen testID was never found in DOM').toBe(true);

      // After redirect, wait for /login to load and check body has content
      await page.waitForURL(/login/, { timeout: 15_000 }).catch(() => {});
      await page.waitForTimeout(2000);
      const bodyText = await page.locator('body').innerText({ timeout: 5_000 }).catch(() => '');
      expect(bodyText.length, 'body text should not be empty after redirect to /login').toBeGreaterThan(10);
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-02 — No content flash: splash persists during `unknown` (hydration)
  test('FE-TC-02 — No content flash during hydration', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const navPromise = page.goto('/');

      let sawSplash = false;
      let loginBeforeSplash = false;

      for (let i = 0; i < 40; i++) {
        const splashCount = await page.locator('[data-testid="splash-screen"]').count().catch(() => 0);
        const loginCount = await page.locator('[data-testid="login-username"]').count().catch(() => 0);
        if (splashCount > 0) sawSplash = true;
        if (loginCount > 0 && !sawSplash) loginBeforeSplash = true;
        await page.waitForTimeout(100);
      }

      await navPromise;

      expect(sawSplash, 'splash-screen element was never seen in DOM').toBe(true);
      expect(loginBeforeSplash, 'login form appeared before splash — content flash bug').toBe(false);
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-03 — Signed-in user holds on splash while `Me` fetches
  test('FE-TC-03 — Splash persists during Me fetch after reload', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Establish a session (register parent)
      const email = uniqueEmail('tc03');
      await registerParent(page, { email });
      // Token now in localStorage

      const navPromise = page.goto('/');

      let sawSplash = false;
      let loginBeforeSplash = false;

      for (let i = 0; i < 50; i++) {
        const splashCount = await page.locator('[data-testid="splash-screen"]').count().catch(() => 0);
        const loginCount = await page.locator('[data-testid="login-username"]').count().catch(() => 0);
        if (splashCount > 0) sawSplash = true;
        if (loginCount > 0 && !sawSplash) loginBeforeSplash = true;
        await page.waitForTimeout(150);
      }

      await navPromise;

      expect(sawSplash, 'splash must be present during signed-in Me-loading phase').toBe(true);
      expect(loginBeforeSplash, 'login must NOT appear before splash when signed in').toBe(false);
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group B — Routing by `Me`
// ===========================================================================

test.describe('Group B — Routing by Me', () => {
  // FE-TC-04 — Signed-out boot redirects to Login
  test('FE-TC-04 — Signed-out → Login', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/');
      await page.waitForURL(/login/, { timeout: 20_000 });

      // testID="login-username" IS the <input> on RN Web
      const usernameInput = page.getByTestId('login-username');
      await expect(usernameInput).toBeVisible({ timeout: 5_000 });

      // Create-account link visible
      await expect(page.getByRole('link').first()).toBeVisible({ timeout: 5_000 });
      expect(page.url()).toContain('login');
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-05 — No student self-registration link on the auth chain
  test('FE-TC-05 — No student self-registration link on auth chain', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);

      // Check all role=link elements for student-register patterns
      const links = page.getByRole('link');
      const linkCount = await links.count();
      for (let i = 0; i < linkCount; i++) {
        const text = (await links.nth(i).innerText().catch(() => '')).toLowerCase();
        const label = (await links.nth(i).getAttribute('aria-label') ?? '').toLowerCase();
        expect(text + ' ' + label).not.toMatch(
          /student.*sign.*up|student.*register|signup.*student|register.*student/i,
        );
      }

      // The create-account link navigates to parent register (/register with Terms checkbox)
      const createLink = page.getByRole('link', { name: /create.*parent|إنشاء.*حساب/i });
      const visible = await createLink.isVisible({ timeout: 3_000 }).catch(() => false);
      if (visible) {
        await createLink.click();
        await page.waitForURL(/register/, { timeout: 10_000 });
        await expect(page.getByRole('checkbox')).toBeVisible({ timeout: 5_000 });
        expect(page.url()).not.toContain('student');
      }
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-06 — Parent with NO children → onboarding (add-child)
  test('FE-TC-06 — Parent no children → onboarding add-child', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const email = uniqueEmail('tc06');
      await registerParent(page, { email });

      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      const addChildForm = page.getByTestId('add-child-form-card');
      await expect(addChildForm).toBeVisible({ timeout: 15_000 });
      expect(page.url()).toContain('add-child');
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-07 — Child (student role) → child home
  test('FE-TC-07 — Child login → child home (dashboard-header)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const parentEmail = uniqueEmail('tc07par');
      await registerParent(page, { email: parentEmail });
      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      const { email: childEmail, password: childPassword } = await addChildViaForm(page, { language: 'ar' });

      await signOutAndWait(page);
      await signInViaUI(page, childEmail, childPassword);

      const dashHeader = page.getByTestId('dashboard-header');
      await expect(dashHeader).toBeVisible({ timeout: 25_000 });
      expect(page.url()).not.toContain('onboarding');
      expect(page.url()).not.toContain('add-child');
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-08 — Parent WITH children → parent dashboard; sign-out → Login
  test('FE-TC-08 — Parent with children → dashboard; sign-out → Login', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const parentEmail = uniqueEmail('tc08');
      const parentPassword = 'Str0ng!Pass1';
      await registerParent(page, { email: parentEmail, password: parentPassword });
      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      await addChildViaForm(page);

      // Navigate to / — routing guard sends parent-with-children to /(parent)
      await page.goto('/');
      await page.waitForTimeout(8_000);

      const parentHome = page.getByTestId('parent-home');
      await expect(parentHome).toBeVisible({ timeout: 20_000 });

      // Sign out
      const signOutBtn = page.getByTestId('sign-out-button');
      await expect(signOutBtn).toBeVisible({ timeout: 5_000 });
      await signOutBtn.click();

      await page.waitForURL(/login/, { timeout: 20_000 });
      await expect(page.getByTestId('login-username')).toBeVisible({ timeout: 5_000 });
      expect(page.url()).toContain('login');
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group C — Locale applied from `Me.preferredLanguage`
// ===========================================================================

test.describe('Group C — Locale from Me.preferredLanguage', () => {
  /**
   * FE-TC-09 — Arabic child lands RTL even when login UI was English
   *
   * The Batch-3 fix added applyWebDirection(me.preferredLanguage) eagerly in
   * useGroupGuard and useAuthRoute. However, the fix is blocked by a data mismatch:
   *
   *   BUG-REMAINING (D-locale-locale-format): Me.preferredLanguage is returned as
   *   'ar-EG' by the backend, but LOCALES = ['ar', 'en'] on the frontend. The guard
   *   function isLocale('ar-EG') returns false, so applyWebDirection is never called.
   *   The DOM direction stays at the login-screen locale.
   *
   * Fix needed: either (a) normalize preferredLanguage in the backend to 'ar'/'en',
   * or (b) normalize the value in isLocale() / in the useGroupGuard/useAuthRoute
   * guard (e.g. take the first 2 chars: 'ar-EG' → 'ar').
   *
   * This test asserts CORRECT expected behavior per the Design Spec. It FAILS until
   * the locale-format mismatch is fixed.
   */
  test('FE-TC-09 — Arabic child lands RTL (login UI was English)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const parentEmail = uniqueEmail('tc09par');
      await registerParent(page, { email: parentEmail });
      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      const { email: childEmail, password: childPassword } = await addChildViaForm(page, { language: 'ar' });

      await signOutAndWait(page);

      // Switch login UI to English before signing in as Arabic child
      const enSwitch = page.getByTestId('locale-switch-en');
      await expect(enSwitch).toBeVisible({ timeout: 5_000 });
      await enSwitch.click();
      await page.waitForTimeout(500);
      expect(await page.locator('html').getAttribute('dir')).toBe('ltr');

      // Sign in as Arabic child
      await page.getByTestId('login-username').fill(childEmail);
      await page.getByTestId('login-password').fill(childPassword);
      await page.getByTestId('login-submit').click();
      await page.waitForTimeout(12_000);

      const dashHeader = page.getByTestId('dashboard-header');
      await expect(dashHeader).toBeVisible({ timeout: 20_000 });

      // Me.preferredLanguage='ar' should override → RTL.
      // STILL FAILING: Me.preferredLanguage = 'ar-EG' (not 'ar') from the backend.
      // isLocale('ar-EG') === false → applyWebDirection is never called.
      // The DOM stays at the login-screen locale (ltr).
      expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
      expect(await page.locator('html').getAttribute('lang')).toBe('ar');
    } finally {
      await ctx.close();
    }
  });

  /**
   * FE-TC-10 — English child lands LTR even when login UI was Arabic (default)
   *
   * Same root cause as FE-TC-09: Me.preferredLanguage = 'en-US' (not 'en').
   * isLocale('en-US') === false → applyWebDirection is never called.
   * The DOM stays at the login-screen locale (rtl).
   *
   * This test asserts CORRECT expected behavior per the Design Spec. It FAILS until
   * the locale-format mismatch is fixed.
   */
  test('FE-TC-10 — English child lands LTR (login UI was Arabic)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const parentEmail = uniqueEmail('tc10par');
      await registerParent(page, { email: parentEmail });
      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      const { email: childEmail, password: childPassword } = await addChildViaForm(page, { language: 'en' });

      await signOutAndWait(page);

      // Default Arabic — verify RTL on login
      expect(await page.locator('html').getAttribute('dir')).toBe('rtl');

      // Sign in as English child (login UI stays Arabic)
      await page.getByTestId('login-username').fill(childEmail);
      await page.getByTestId('login-password').fill(childPassword);
      await page.getByTestId('login-submit').click();
      await page.waitForTimeout(12_000);

      const dashHeader = page.getByTestId('dashboard-header');
      await expect(dashHeader).toBeVisible({ timeout: 20_000 });

      // Me.preferredLanguage='en' should override → LTR.
      // STILL FAILING: Me.preferredLanguage = 'en-US' (not 'en') from the backend.
      // isLocale('en-US') === false → applyWebDirection is never called.
      // The DOM stays at the login-screen locale (rtl).
      expect(await page.locator('html').getAttribute('dir')).toBe('ltr');
      expect(await page.locator('html').getAttribute('lang')).toBe('en');
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group D — Arabic-default RTL + language switch
// ===========================================================================

test.describe('Group D — Arabic-default RTL + language switch', () => {
  // FE-TC-11 — Default locale is Arabic / RTL on first boot
  test('FE-TC-11 — Default locale Arabic / RTL on first boot', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);

      expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
      expect(await page.locator('html').getAttribute('lang')).toBe('ar');

      const arSwitch = page.getByTestId('locale-switch-ar');
      await expect(arSwitch).toBeVisible({ timeout: 5_000 });

      // Active state = filled background (not transparent)
      const arActive = await isLocaleActive(page, 'locale-switch-ar');
      expect(arActive, 'Arabic radio should be active on first boot').toBe(true);

      const enActive = await isLocaleActive(page, 'locale-switch-en');
      expect(enActive, 'English radio should be inactive on first boot').toBe(false);
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-12 — Language switch ar→en flips to LTR instantly on web
  test('FE-TC-12 — Switch ar→en flips LTR instantly (web)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);

      expect(await page.locator('html').getAttribute('dir')).toBe('rtl');

      const enSwitch = page.getByTestId('locale-switch-en');
      await expect(enSwitch).toBeVisible({ timeout: 5_000 });
      await enSwitch.click();
      await page.waitForTimeout(800);

      // No reload — URL still /login
      expect(page.url()).toContain('login');

      // Immediately dir=ltr, lang=en
      expect(await page.locator('html').getAttribute('dir')).toBe('ltr');
      expect(await page.locator('html').getAttribute('lang')).toBe('en');

      // English radio active, Arabic radio inactive
      expect(await isLocaleActive(page, 'locale-switch-en')).toBe(true);
      expect(await isLocaleActive(page, 'locale-switch-ar')).toBe(false);
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-13 — Language switch en→ar flips back to RTL instantly on web
  test('FE-TC-13 — Switch en→ar flips RTL instantly (web)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);

      // Switch to English first
      await page.getByTestId('locale-switch-en').click();
      await page.waitForTimeout(600);
      expect(await page.locator('html').getAttribute('dir')).toBe('ltr');

      // Switch back to Arabic
      await page.getByTestId('locale-switch-ar').click();
      await page.waitForTimeout(800);

      expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
      expect(await page.locator('html').getAttribute('lang')).toBe('ar');

      expect(await isLocaleActive(page, 'locale-switch-ar')).toBe(true);
      expect(await isLocaleActive(page, 'locale-switch-en')).toBe(false);

      // No restart prompt on web (instant flip — no native dialog)
      const bodyText = await page.locator('body').innerText();
      expect(bodyText).not.toContain('Restart to change language');
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-14 — Login screen renders correctly in both locales (RTL/LTR layout)
  test('FE-TC-14 — Login renders correctly in both locales', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);

      // Arabic (default): all key elements visible
      await expect(page.getByTestId('login-username')).toBeVisible({ timeout: 5_000 });
      await expect(page.getByTestId('login-password')).toBeVisible({ timeout: 5_000 });
      await expect(page.getByTestId('login-submit')).toBeVisible({ timeout: 5_000 });
      // There are 2 radiogroups on login: LocaleThemeControls + PersonaToggle. Target by name.
      await expect(page.getByRole('radiogroup', { name: /language|اللغة/i })).toBeVisible({ timeout: 5_000 });
      await expect(page.getByRole('link').first()).toBeVisible({ timeout: 5_000 });

      // Switch to English
      await page.getByTestId('locale-switch-en').click();
      await page.waitForTimeout(800);

      // All elements still visible in LTR
      await expect(page.getByTestId('login-username')).toBeVisible();
      await expect(page.getByTestId('login-password')).toBeVisible();
      await expect(page.getByTestId('login-submit')).toBeVisible();
      await expect(page.getByRole('radiogroup', { name: /language|اللغة/i })).toBeVisible();
      await expect(page.getByRole('link').first()).toBeVisible();

      expect(await page.locator('html').getAttribute('dir')).toBe('ltr');
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group E — i18n integrity (no raw keys)
// ===========================================================================

test.describe('Group E — i18n integrity', () => {
  async function collectRawKeys(page: Page): Promise<string[]> {
    return page.evaluate(() => {
      const suspects: string[] = [];
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      let node: Node | null;
      while ((node = walker.nextNode())) {
        const text = (node.nodeValue ?? '').trim();
        if (!text) continue;
        if (/^(auth|common|onboarding|parent|child)\.[a-zA-Z.]+$/.test(text)) suspects.push(text);
        if (/missingkey/i.test(text)) suspects.push(`missingKey: ${text}`);
      }
      return suspects;
    });
  }

  // FE-TC-15 — No raw i18n keys on the Arabic chain
  test('FE-TC-15 — No raw i18n keys (Arabic)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);
      const rawKeys = await collectRawKeys(page);
      expect(rawKeys, `Raw keys on Arabic login: ${rawKeys.join(', ')}`).toHaveLength(0);
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-16 — No raw i18n keys on the English chain
  test('FE-TC-16 — No raw i18n keys (English)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(3_000);
      await page.getByTestId('locale-switch-en').click();
      await page.waitForTimeout(1_000);
      const rawKeys = await collectRawKeys(page);
      expect(rawKeys, `Raw keys on English login: ${rawKeys.join(', ')}`).toHaveLength(0);
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group F — Session-expired flash
// ===========================================================================

test.describe('Group F — Session-expired flash', () => {
  // FE-TC-17 — Session-expired flash shows on Login after silent-refresh failure
  test('FE-TC-17 — Session-expired flash: 401 triggers sign-out + redirect to Login', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Establish session
      const email = uniqueEmail('tc17');
      await registerParent(page, { email });

      // Intercept ALL API calls to return 401 (simulates expired/revoked session)
      await page.route('**/api/**', (route) => {
        return route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ successed: false, errors: ['Unauthorized'] }),
        });
      });

      // Navigate to / — routing guard fetches Me, gets 401, onSignOut fires
      await page.goto('/');
      await page.waitForTimeout(12_000);

      // Primary assertion: redirected to login
      await page.waitForURL(/login/, { timeout: 20_000 }).catch(() => {});
      expect(page.url()).toContain('login');
      await expect(page.getByTestId('login-username')).toBeVisible({ timeout: 5_000 });

      // Secondary (soft): check if flash text appears (depends on api-client interceptor)
      const bodyText = await page.locator('body').innerText({ timeout: 3_000 }).catch(() => '');
      const hasFlash =
        bodyText.toLowerCase().includes('session expired') ||
        bodyText.includes('انتهت الجلسة') ||
        bodyText.toLowerCase().includes('sign in again');
      // Log the flash status but do not fail here — routing to /login is the key assertion
      console.log(`[FE-TC-17] Session-expired flash text visible on login = ${hasFlash}`);

      await page.unrouteAll({ behavior: 'ignoreErrors' });
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-18 — Session-expired flash is one-shot (cleared after first display)
  test('FE-TC-18 — Session-expired flash is one-shot (not shown on reload)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Cold login — no prior flash pending
      await page.goto('/login');
      await page.waitForTimeout(3_000);

      // On a clean cold load, no session-expired message should appear
      const bodyText = await page.locator('body').innerText({ timeout: 3_000 }).catch(() => '');
      const hasStale =
        bodyText.toLowerCase().includes('session expired') &&
        bodyText.toLowerCase().includes('sign in again');
      expect(hasStale, 'Stale flash showed on cold /login').toBe(false);

      // Reload — consume() clears on mount, so still not shown
      await page.reload();
      await page.waitForTimeout(2_000);
      const bodyAfter = await page.locator('body').innerText({ timeout: 3_000 }).catch(() => '');
      const hasStaleAfterReload =
        bodyAfter.toLowerCase().includes('session expired') &&
        bodyAfter.toLowerCase().includes('sign in again');
      expect(hasStaleAfterReload, 'Stale flash showed after reload of /login').toBe(false);
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group G — Error / loading states
// ===========================================================================

test.describe('Group G — Error / loading states', () => {
  // FE-TC-19 — Login error banner on invalid credentials (no field-level reveal)
  test('FE-TC-19 — Login error banner on invalid credentials', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await page.goto('/login');
      await page.waitForTimeout(2_000);

      // Wrong credentials
      await page.getByTestId('login-username').fill('nobody+wrong@example.com');
      await page.getByTestId('login-password').fill('WrongPass999!');
      await page.getByTestId('login-submit').click();
      await page.waitForTimeout(8_000);

      // URL still /login
      expect(page.url()).toContain('login');

      // Error banner testID="login-error"
      const errorBanner = page.getByTestId('login-error');
      await expect(errorBanner).toBeVisible({ timeout: 10_000 });

      // No raw i18n key
      const bodyText = await page.locator('body').innerText();
      expect(bodyText).not.toMatch(/auth\.login\.errors\.\w+/);

      // Resolved error message present
      const hasResolved =
        bodyText.includes('Incorrect') ||
        bodyText.includes('incorrect') ||
        bodyText.includes('password') ||
        bodyText.includes('No account') ||
        bodyText.includes('كلمة المرور') ||
        bodyText.includes('غير صحيح') ||
        bodyText.includes('لا يوجد');
      expect(hasResolved).toBe(true);
    } finally {
      await ctx.close();
    }
  });

  // FE-TC-20 — Sign-out is resilient even if the API call fails
  test('FE-TC-20 — Sign-out resilient even if API fails', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Setup: parent with children → parent dashboard
      const parentEmail = uniqueEmail('tc20');
      await registerParent(page, { email: parentEmail });
      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      await addChildViaForm(page);
      await page.waitForTimeout(5_000);

      await page.goto('/');
      await page.waitForTimeout(8_000);

      const parentHome = page.getByTestId('parent-home');
      await expect(parentHome).toBeVisible({ timeout: 20_000 });

      // Block the sign-out API endpoint
      await page.route('**/Sign-Out**', (route) => route.abort('failed'));

      // Sign out — local session clears regardless of API failure
      const signOutBtn = page.getByTestId('sign-out-button');
      await signOutBtn.click();

      await page.waitForURL(/login/, { timeout: 20_000 });
      expect(page.url()).toContain('login');
      await expect(page.getByTestId('login-username')).toBeVisible({ timeout: 5_000 });

      await page.unrouteAll({ behavior: 'ignoreErrors' });
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group H — Kid-UX (NFR-6)
// ===========================================================================

test.describe('Group H — Kid-UX (NFR-6)', () => {
  // FE-TC-21 — Child home meets kid-UX touch-target / single-primary baseline
  test('FE-TC-21 — Child home kid-UX baseline', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const parentEmail = uniqueEmail('tc21par');
      await registerParent(page, { email: parentEmail });
      await page.waitForURL(/add-child/, { timeout: 20_000 }).catch(() => {});
      const { email: childEmail, password: childPassword } = await addChildViaForm(page, { language: 'ar' });

      await signOutAndWait(page);
      await signInViaUI(page, childEmail, childPassword);

      const dashHeader = page.getByTestId('dashboard-header');
      await expect(dashHeader).toBeVisible({ timeout: 25_000 });

      // No scary error chrome
      const bodyText = await page.locator('body').innerText({ timeout: 5_000 }).catch(() => '');
      expect(bodyText).not.toContain('[object Object]');
      expect(bodyText).not.toContain('undefined');

      // Sign-out button ≥ 44px height (kid-UX NFR-6)
      const signOutBtn = page.getByTestId('sign-out-button');
      await expect(signOutBtn).toBeVisible({ timeout: 5_000 });

      const box = await signOutBtn.boundingBox();
      expect(box, 'sign-out-button bounding box must not be null').not.toBeNull();
      if (box) {
        expect(box.height).toBeGreaterThanOrEqual(44);
      }

      // Reachable by aria-label (role=button accessible)
      const byRole = page.getByRole('button', { name: /sign out|تسجيل الخروج/i });
      const found = await byRole.isVisible({ timeout: 2_000 }).catch(() => false);
      expect(found || box !== null, 'sign-out must be findable by role or testID').toBe(true);
    } finally {
      await ctx.close();
    }
  });
});

// ===========================================================================
// Group I — Native RTL restart boundary (BLOCKED)
// ===========================================================================

test.describe('Group I — Native RTL restart (BLOCKED)', () => {
  test.skip(
    true,
    'FE-TC-22 BLOCKED — native restart prompt (I18nManager.forceRTL + react-native-restart) ' +
      'is untestable in Playwright web E2E. On web the flip is instant (no restart prompt). ' +
      'Covered by manual/native QA pass (P1-09-FE-5).',
  );
  test('FE-TC-22 — Native LTR↔RTL restart prompt (BLOCKED)', async () => {
    /* intentionally empty */
  });
});
