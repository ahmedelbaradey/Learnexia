/**
 * P1-01-FE — Register screen E2E tests (web PWA)
 *
 * Implements every FE-TC-* case from docs/qc/P1-01-FE/frontend-test-cases.md.
 *
 * Selector strategy (testIDs are available on all key elements):
 *   - Full name:    getByTestId('register-fullname')
 *   - Country:      getByTestId('register-country')   [Select, renders as combobox]
 *   - Email:        getByTestId('register-email')
 *   - Password:     getByTestId('register-password')  [also input[type="password"] inside]
 *   - Terms:        getByTestId('register-terms')
 *   - Submit:       getByTestId('register-submit')
 *   - Sign-in link: getByTestId('register-sign-in-link')
 *   - Error banner: getByTestId('register-error')
 *
 * RTL ground truth (applyWebDirection DOES NOT set html[dir]):
 *   applyWebDirection() intentionally sets document.documentElement.lang only,
 *   NOT dir. RN Web handles RTL entirely through component-level props
 *   (writingDirection, flexDirection row-reverse, textAlign). Assertions for
 *   RTL must target a known element's computed direction, NOT html[dir].
 *
 * TWO-STEP WIZARD (register.tsx):
 *   Step 1 = parent account form (URL stays /register).
 *   Step 2 = add-child inline, still at /register — no navigation to add-child route.
 *   After successful step-1 submit: wait for onboarding-add-child-tile (step 2 marker).
 *
 * Known RN-Web limitations (documented in execution-report.md):
 *   - checkbox `aria-checked` is NOT set (accessibilityState not translated to aria-*).
 *     Checked state is detected visually: filled background (#4f46e5/primary) + "✓" text.
 *   - Button `disabled`/`aria-busy` NOT reflected as HTML attributes; loading state is
 *     detected via `pointer-events: none` + `opacity: 0.4`.
 *   - Form validation errors: `onTouched` mode with RN Web blur event doesn't fire from
 *     Playwright's .blur() call. Validation errors appear after a submit attempt OR
 *     after clicking a different focusable element within the same React tree.
 *   - ServerErrorBanner uses `aria-label` (not standard [aria-live]); detect via
 *     testID or page text.
 *   - Country Select renders options inline in the DOM (not role=option/listbox);
 *     selected via text locator after expanding.
 *   - Non-existent Expo Router routes (e.g. /register-student) never resolve — the
 *     page hangs waiting for load. Navigation assertions use waitForURL with a short
 *     timeout and catch TimeoutError.
 */

import { test, expect } from '@playwright/test';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Unique parent email per run to avoid duplicate-account collisions. */
function uniqueEmail(): string {
  return `parent+e2e+${Date.now()}@example.com`;
}

/**
 * Fill the register form with valid data using stable testID selectors.
 * Selects Saudi Arabia (السعودية) as the country — first option in the panel.
 * Leaves Terms unchecked unless `checkTerms=true`.
 */
async function fillValidForm(
  page: import('@playwright/test').Page,
  opts: { email?: string; password?: string; checkTerms?: boolean } = {},
) {
  const { email = uniqueEmail(), password = 'Str0ng!Pass', checkTerms = false } = opts;

  // Full name
  await page.getByTestId('register-fullname').fill('Parent Tester');

  // Country Select — click the container to expand the inline panel, then pick first option
  const countrySelect = page.getByTestId('register-country');
  await countrySelect.click();
  await page.waitForTimeout(500);
  // Saudi Arabia is the first country listed in the inline panel
  const firstOption = page.locator(':text-is("السعودية")').first();
  await firstOption.waitFor({ state: 'visible', timeout: 5000 });
  await firstOption.click();
  await page.waitForTimeout(300);

  // Email
  await page.getByTestId('register-email').fill(email);

  // Password
  await page.locator('input[type="password"]').first().fill(password);

  // Terms checkbox
  if (checkTerms) {
    await page.getByTestId('register-terms').click();
    await page.waitForTimeout(300);
  }
}

/**
 * Assert checkbox visual state: checked = primary-color background + checkmark,
 * unchecked = transparent background + no checkmark.
 * RN Web does NOT translate `accessibilityState.checked` → `aria-checked`.
 */
async function getCheckboxCheckedVisual(page: import('@playwright/test').Page): Promise<boolean> {
  return page.evaluate(() => {
    const checkbox = document.querySelector('[data-testid="register-terms"]');
    if (!checkbox) return false;
    // The inner box (first child div) gets primary background (#4f46e5) when checked
    const innerBox = checkbox.querySelector('div');
    if (!innerBox) return false;
    const bg = window.getComputedStyle(innerBox).backgroundColor;
    // transparent / rgba(0,0,0,0) = unchecked; any other opaque bg = checked
    return bg !== '' && bg !== 'rgba(0, 0, 0, 0)' && !bg.includes('transparent');
  });
}

/**
 * Assert the submit button is in loading state (pointer-events:none + opacity~0.4).
 * RN Web Button does NOT set HTML `disabled` or `aria-busy` attributes.
 */
async function isSubmitButtonLoading(page: import('@playwright/test').Page): Promise<boolean> {
  const submit = page.getByTestId('register-submit');
  const pointerEvents = await submit.evaluate(
    (el: HTMLElement) => window.getComputedStyle(el).pointerEvents,
  );
  const opacity = await submit.evaluate(
    (el: HTMLElement) => window.getComputedStyle(el).opacity,
  );
  return pointerEvents === 'none' && parseFloat(opacity) < 0.7;
}

// ---------------------------------------------------------------------------
// Group A — Happy path & navigation
// ---------------------------------------------------------------------------

test.describe('FE-TC-01 — Form accepts valid input and is submittable', () => {
  test('register form mounts with required fields', async ({ page }) => {
    await page.goto('/register');

    // Form heading visible (second heading — first is the feature panel)
    const heading = page.getByRole('heading').last();
    await expect(heading).toBeVisible({ timeout: 30_000 });

    // Full name field
    await expect(page.getByTestId('register-fullname')).toBeVisible();

    // Country select
    await expect(page.getByTestId('register-country')).toBeVisible();

    // Email field
    await expect(page.getByTestId('register-email')).toBeVisible();

    // Password input
    await expect(page.locator('input[type="password"]').first()).toBeVisible();

    // Terms checkbox
    await expect(page.getByTestId('register-terms')).toBeVisible();

    // Submit button
    await expect(page.getByTestId('register-submit')).toBeVisible();
  });

  test('filling all fields leaves no inline errors and enables submit', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    await fillValidForm(page, { checkTerms: true });

    // Submit button should be enabled (not in loading state)
    const isLoading = await isSubmitButtonLoading(page);
    expect(isLoading).toBe(false);

    // Terms checkbox is visually checked
    const checked = await getCheckboxCheckedVisual(page);
    expect(checked).toBe(true);

    // No validation errors visible in page text
    const pageText = await page.locator('body').innerText();
    expect(pageText).not.toContain('auth.register.errors');
  });
});

test.describe('FE-TC-04 — Successful registration shows step 2 add-child inline', () => {
  /**
   * After a successful register the URL STAYS at /register and the wizard
   * advances to step 2 inline — the add-child tile (testID onboarding-add-child-tile)
   * appears. There is NO navigation to an /add-child route.
   */
  test('POST to real backend succeeds and step 2 add-child UI renders inline', async ({ page }) => {
    const email = uniqueEmail();
    await page.goto('/register');
    await page.waitForTimeout(2000);

    await fillValidForm(page, { email, password: 'Str0ng!Pass1', checkTerms: true });

    // Submit
    await page.getByTestId('register-submit').click();

    // Wait for step 2 marker — the dashed "Add a child" tile renders inline at /register
    await page.getByTestId('onboarding-add-child-tile').waitFor({ state: 'visible', timeout: 15_000 });

    // URL is still /register (no route jump)
    expect(page.url()).toContain('register');
    expect(page.url()).not.toContain('add-child/');

    // Step 2 heading is visible (onboarding.addChild.title)
    const heading = page.getByRole('heading');
    await expect(heading.first()).toBeVisible({ timeout: 5_000 });
  });
});

test.describe('FE-TC-16 — Sign-in link returns to auth entry (role-select)', () => {
  /**
   * Batch A: The register screen's "Sign in" link navigates to /(auth)/login
   * which immediately redirects to /(auth)/role-select (no role param in the
   * URL). So the final URL is /role-select, not /login. The back button on
   * the register screen similarly navigates to /role-select via the auth guard.
   */
  test('sign in link navigates to role-select (auth entry point)', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // The register-sign-in-link is a Text with accessibilityRole="link"
    const signInLink = page.getByTestId('register-sign-in-link');
    await expect(signInLink).toBeVisible();
    await signInLink.click();

    // Lands on /role-select (the new auth entry point after Batch A)
    await page.waitForURL(/role-select|login/, { timeout: 10_000 });
    // Either role-select or login URL (login immediately redirects to role-select)
    const url = page.url();
    expect(url.includes('role-select') || url.includes('login')).toBe(true);
  });

  test('back button also returns to auth entry', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // First button is the ScreenHeader back button
    const backBtn = page.getByRole('button').first();
    await expect(backBtn).toBeVisible();
    await backBtn.click();

    await page.waitForURL(/role-select|login/, { timeout: 10_000 });
    const url = page.url();
    expect(url.includes('role-select') || url.includes('login')).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Group B — Consent gate
// ---------------------------------------------------------------------------

test.describe('FE-TC-02 — Submitting without Terms is blocked', () => {
  test('no navigation and termsRequired error shown when Terms unchecked', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Fill everything valid but leave Terms unchecked
    await fillValidForm(page, { checkTerms: false });

    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(1000);

    // URL still /register
    expect(page.url()).toContain('register');
    // Must NOT have navigated to add-child route
    expect(page.url()).not.toContain('add-child/');

    // Step 2 tile must NOT be visible (not navigated)
    const addChildTile = page.getByTestId('onboarding-add-child-tile');
    await expect(addChildTile).not.toBeVisible();

    // Inline error text is visible — human-readable (not the raw key)
    const pageText = await page.locator('body').innerText();
    expect(pageText).not.toContain('auth.register.errors.termsRequired');
    // Arabic: "يرجى الموافقة على الشروط للمتابعة." / English: "Please accept the Terms to continue."
    const hasTermsError =
      pageText.includes('يرجى الموافقة على الشروط') ||
      pageText.includes('Please accept the Terms to continue');
    expect(hasTermsError).toBe(true);

    // Checkbox is still visually unchecked
    const checked = await getCheckboxCheckedVisual(page);
    expect(checked).toBe(false);
  });
});

test.describe('FE-TC-03 — Checking Terms toggles state and clears consent error', () => {
  test('checking terms after a blocked submit enables submit and clears error', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Trigger the consent error first
    await fillValidForm(page, { checkTerms: false });
    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(1000);

    // Error should be visible
    const pageTextBefore = await page.locator('body').innerText();
    const hasError =
      pageTextBefore.includes('يرجى الموافقة') ||
      pageTextBefore.includes('Please accept the Terms');
    expect(hasError).toBe(true);

    // Now check the Terms checkbox
    await page.getByTestId('register-terms').click();
    await page.waitForTimeout(300);

    // Checkbox is now visually checked
    const checked = await getCheckboxCheckedVisual(page);
    expect(checked).toBe(true);

    // Error text is gone
    const pageTextAfter = await page.locator('body').innerText();
    const stillHasError =
      pageTextAfter.includes('يرجى الموافقة على الشروط للمتابعة') ||
      pageTextAfter.includes('Please accept the Terms to continue');
    expect(stillHasError).toBe(false);

    // Submit button is not in loading state (i.e. interactable)
    const loading = await isSubmitButtonLoading(page);
    expect(loading).toBe(false);
  });
});

test.describe('FE-TC-19 — Parent-only consent banner is present', () => {
  test('parent guardian only banner renders', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // The ParentOnlyBanner contains a parent-only title and body.
    // Arabic: "ولي أمر / وصي قانوني فقط" / English: "Parent / Guardian only"
    const pageText = await page.locator('body').innerText();
    const hasParentBanner =
      pageText.includes('ولي أمر / وصي قانوني فقط') ||
      pageText.includes('Parent / Guardian only');
    expect(hasParentBanner).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Group C — Field validation
// ---------------------------------------------------------------------------

test.describe('FE-TC-07 — Invalid email shows localized inline error', () => {
  test('not-an-email triggers human-readable error after submit, not raw key', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Fill all fields valid except email
    await page.getByTestId('register-fullname').fill('Parent Tester');
    await page.getByTestId('register-country').click();
    await page.waitForTimeout(500);
    await page.locator(':text-is("السعودية")').first().click();
    await page.waitForTimeout(300);

    // Invalid email
    await page.getByTestId('register-email').fill('not-an-email');
    await page.locator('input[type="password"]').first().fill('Str0ng!Pass');
    await page.getByTestId('register-terms').click();
    await page.waitForTimeout(300);

    // Attempt submit to trigger validation (onTouched mode fires on submit for RN Web)
    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(1000);

    const pageText = await page.locator('body').innerText();
    // Raw key must NOT appear
    expect(pageText).not.toContain('auth.register.errors.invalidEmail');
    // Resolved text must appear (Arabic or English)
    const hasError =
      pageText.includes('يرجى إدخال بريد إلكتروني صحيح') ||
      pageText.includes('Please enter a valid email address');
    expect(hasError).toBe(true);

    // No navigation — still at /register step 1 (no add-child tile)
    expect(page.url()).toContain('register');
    await expect(page.getByTestId('onboarding-add-child-tile')).not.toBeVisible();
  });
});

test.describe('FE-TC-08 — Password shorter than 6 chars is blocked client-side', () => {
  test('4-char password triggers human-readable weakPassword error after submit', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Fill all fields valid except password (too short)
    await page.getByTestId('register-fullname').fill('Parent Tester');
    await page.getByTestId('register-country').click();
    await page.waitForTimeout(500);
    await page.locator(':text-is("السعودية")').first().click();
    await page.waitForTimeout(300);
    await page.getByTestId('register-email').fill(uniqueEmail());
    await page.locator('input[type="password"]').first().fill('Ab1!'); // 4 chars — under min(6)
    await page.getByTestId('register-terms').click();
    await page.waitForTimeout(300);

    // Submit to trigger validation
    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(1500);

    const pageText = await page.locator('body').innerText();
    // Raw key must NOT appear
    expect(pageText).not.toContain('auth.register.errors.weakPassword');
    // Resolved text — `auth.register.errors.weakPassword` resolves to:
    // EN: "Password must be at least 6 characters with uppercase, lowercase, number, and special character."
    // AR: "يجب أن تحتوي كلمة المرور على 6 أحرف على الأقل مع حروف كبيرة وصغيرة ورقم وحرف خاص."
    const hasPasswordError =
      pageText.includes('Password must be at least') ||
      pageText.includes('يجب أن تحتوي كلمة المرور') ||
      pageText.includes('6 characters');
    expect(hasPasswordError).toBe(true);

    // No navigation — still at /register step 1
    expect(page.url()).toContain('register');
    await expect(page.getByTestId('onboarding-add-child-tile')).not.toBeVisible();
  });
});

test.describe('FE-TC-09 — Country is required', () => {
  test('empty country selection shows localized error', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Fill everything except country
    await page.getByTestId('register-fullname').fill('Parent Tester');
    await page.getByTestId('register-email').fill(uniqueEmail());
    await page.locator('input[type="password"]').first().fill('Str0ng!Pass');
    await page.getByTestId('register-terms').click();
    await page.waitForTimeout(300);

    // Submit without selecting country
    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(1000);

    // Still on register step 1
    expect(page.url()).toContain('register');
    await expect(page.getByTestId('onboarding-add-child-tile')).not.toBeVisible();

    const pageText = await page.locator('body').innerText();
    // Raw key must NOT appear
    expect(pageText).not.toContain('auth.register.errors.countryRequired');
    // Resolved text must appear
    const hasCountryError =
      pageText.includes('يرجى اختيار دولتك') ||
      pageText.includes('Please select your country');
    expect(hasCountryError).toBe(true);
  });
});

test.describe('FE-TC-10 — Country picker opens and selection sticks', () => {
  test('clicking country select shows options; selecting one updates display value', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    const countrySelect = page.getByTestId('register-country');
    // Initially shows placeholder text: "اختر الدولة" / "Select country"
    const placeholderText = await countrySelect.innerText();
    expect(placeholderText).toContain('اختر الدولة');

    // Click to expand
    await countrySelect.click();
    await page.waitForTimeout(500);

    // Saudi Arabia option is now visible in the inline panel
    const saudi = page.locator(':text-is("السعودية")').first();
    await expect(saudi).toBeVisible({ timeout: 5_000 });

    // Select it
    await saudi.click();
    await page.waitForTimeout(300);

    // Country select now shows the selected country name (not the placeholder)
    const afterText = await countrySelect.innerText();
    expect(afterText).not.toContain('اختر الدولة');
    expect(afterText).toContain('السعودية');
  });
});

test.describe('FE-TC-12 — Password input is masked', () => {
  test('password field has type=password by default', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    const passwordInput = page.locator('input[type="password"]').first();
    await expect(passwordInput).toBeVisible();

    // Confirm it is truly type=password
    const type = await passwordInput.getAttribute('type');
    expect(type).toBe('password');
  });

  test('show/hide eye button toggles password visibility', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    await page.locator('input[type="password"]').first().fill('Str0ng!Pass');

    // The password field (register-password) contains the show/hide toggle button.
    // The toggle is the only button inside the password field container.
    // We find it by aria-label: showLabel = t('auth.login.showPassword') → "Show password" / "إظهار كلمة المرور"
    const allButtons = page.getByRole('button');
    const count = await allButtons.count();
    let eyeButton: import('@playwright/test').Locator | null = null;
    for (let i = 0; i < count; i++) {
      const btn = allButtons.nth(i);
      const ariaLabel = await btn.getAttribute('aria-label').catch(() => null);
      if (
        ariaLabel?.toLowerCase().includes('show') ||
        ariaLabel?.includes('إظهار')
      ) {
        eyeButton = btn;
        break;
      }
    }
    expect(eyeButton).not.toBeNull();
    if (!eyeButton) return;

    const ariaLabel = await eyeButton.getAttribute('aria-label');
    // Should be the show-password button
    const isShowPasswordButton =
      ariaLabel?.toLowerCase().includes('show') ||
      ariaLabel?.includes('إظهار') ||
      ariaLabel?.includes('Show password');
    expect(isShowPasswordButton).toBe(true);

    // Click to show
    await eyeButton.click();
    await page.waitForTimeout(300);

    // After toggle, the input should switch to type=text (password revealed)
    const inputs = page.locator('input');
    const inputCount = await inputs.count();
    let hasTextType = false;
    for (let i = 0; i < inputCount; i++) {
      const type = await inputs.nth(i).getAttribute('type');
      if (type === 'text') {
        const val = await inputs.nth(i).inputValue().catch(() => '');
        if (val.length > 0) {
          hasTextType = true;
          break;
        }
      }
    }
    // The aria-label for the button should flip to "hide"
    const newAriaLabel = await eyeButton.getAttribute('aria-label');
    const isHideButton =
      newAriaLabel?.toLowerCase().includes('hide') ||
      newAriaLabel?.includes('إخفاء') ||
      hasTextType;
    expect(isHideButton).toBe(true);
  });
});

test.describe('FE-TC-11 — Submit shows pending/loading state and prevents double-submit', () => {
  test('submit button enters loading state (pointer-events:none) during in-flight request', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    await fillValidForm(page, { checkTerms: true });

    let requestIntercepted = false;
    // Intercept and delay the register API call
    await page.route('**/Register-Parent*', async (route) => {
      requestIntercepted = true;
      await new Promise((resolve) => setTimeout(resolve, 4000));
      await route.continue();
    });

    const submit = page.getByTestId('register-submit');

    // Click submit
    await submit.click();

    // Wait for the loading state to kick in (React state update is async)
    await page.waitForTimeout(400);

    // The button should be in loading state: pointer-events:none
    const pointerEvents = await submit.evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).pointerEvents,
    );
    expect(pointerEvents).toBe('none');

    // Opacity should be reduced (dimmed during loading)
    const opacity = await submit.evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).opacity,
    );
    expect(parseFloat(opacity)).toBeLessThan(0.7);

    // The request was intercepted
    expect(requestIntercepted).toBe(true);

    await page.unrouteAll({ behavior: 'ignoreErrors' });
  });
});

// ---------------------------------------------------------------------------
// Group D — Server-error surfacing
// ---------------------------------------------------------------------------

test.describe('FE-TC-13 — Duplicate email shows localized duplicate-email banner', () => {
  test('registering same email twice shows duplicate-email message', async ({ page }) => {
    const email = `dupe+${Date.now()}@example.com`;

    // First registration — should succeed and advance to step 2
    await page.goto('/register');
    await page.waitForTimeout(2000);
    await fillValidForm(page, { email, password: 'Str0ng!Pass1', checkTerms: true });
    await page.getByTestId('register-submit').click();
    // Step 2 add-child tile appears inline (no route change to add-child/)
    await page.getByTestId('onboarding-add-child-tile').waitFor({ state: 'visible', timeout: 15_000 });

    // Second registration attempt with same email
    await page.goto('/register');
    await page.waitForTimeout(2000);
    await fillValidForm(page, { email, password: 'Str0ng!Pass1', checkTerms: true });
    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(4000);

    // Should stay on register (no step-2 tile visible)
    expect(page.url()).toContain('register');
    await expect(page.getByTestId('onboarding-add-child-tile')).not.toBeVisible();

    // Server error banner appears with duplicate-email resolved text
    // Arabic: "يوجد حساب بهذا البريد الإلكتروني بالفعل."
    // English: "An account with this email already exists."
    const pageText = await page.locator('body').innerText();

    // Raw i18n key must NOT appear
    expect(pageText).not.toContain('auth.register.errors.duplicateEmail');

    // Human-readable duplicate-email message must appear
    const hasDuplicateError =
      pageText.includes('يوجد حساب بهذا البريد الإلكتروني بالفعل') ||
      pageText.includes('An account with this email already exists');
    expect(hasDuplicateError).toBe(true);
  });
});

test.describe('FE-TC-14 — Server-weak password surfaces weak-password banner', () => {
  test('password passing client min(6) but failing server rules shows weak-password banner', async ({
    page,
  }) => {
    // 'abcdef' = 6 chars, passes client min(6) but fails backend's upper/digit/special rule
    await page.goto('/register');
    await page.waitForTimeout(2000);
    await fillValidForm(page, {
      email: uniqueEmail(),
      password: 'abcdef',
      checkTerms: true,
    });

    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(4000);

    // Should stay on register (no step-2 tile)
    expect(page.url()).toContain('register');
    await expect(page.getByTestId('onboarding-add-child-tile')).not.toBeVisible();

    const pageText = await page.locator('body').innerText();
    // Raw key must NOT appear
    expect(pageText).not.toContain('auth.register.errors.weakPassword');
    // Resolved weak-password message must appear
    // EN: "Password must be at least 6 characters with uppercase, lowercase, number, and special character."
    // AR: "يجب أن تحتوي كلمة المرور على 6 أحرف على الأقل مع حروف كبيرة وصغيرة ورقم وحرف خاص."
    const hasWeakPwError =
      pageText.includes('Password must be at least') ||
      pageText.includes('يجب أن تحتوي كلمة المرور') ||
      pageText.includes('uppercase');
    expect(hasWeakPwError).toBe(true);
  });
});

test.describe('FE-TC-15 — Network failure shows generic localized error', () => {
  test('aborted request shows network-error message and re-enables form', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    await fillValidForm(page, { checkTerms: true });

    // Abort the register request to simulate network failure
    await page.route('**/Register-Parent*', (route) => route.abort('failed'));

    await page.getByTestId('register-submit').click();
    await page.waitForTimeout(3000);

    // URL still on register
    expect(page.url()).toContain('register');

    // Error text must NOT be raw i18n key
    const pageText = await page.locator('body').innerText();
    expect(pageText).not.toContain('common.error.networkError');
    expect(pageText).not.toContain('common.error.serverError');

    // Either a generic server error or network error resolved message should appear
    // "Something went wrong. Please try again." / "حدث خطأ ما. يرجى المحاولة مرة أخرى."
    // "No internet connection..." / "لا يوجد اتصال بالإنترنت..."
    const hasErrorMsg =
      pageText.includes('Something went wrong') ||
      pageText.includes('No internet connection') ||
      pageText.includes('حدث خطأ') ||
      pageText.includes('لا يوجد اتصال') ||
      pageText.includes('خطأ ما') ||
      pageText.includes('تعذر');
    expect(hasErrorMsg).toBe(true);

    // Form should be re-enabled after error (button no longer in loading state)
    const loading = await isSubmitButtonLoading(page);
    expect(loading).toBe(false);

    await page.unrouteAll({ behavior: 'ignoreErrors' });
  });
});

// ---------------------------------------------------------------------------
// Group E — RTL / LTR
// ---------------------------------------------------------------------------

test.describe('FE-TC-05 — Arabic default renders the form RTL', () => {
  /**
   * applyWebDirection() intentionally does NOT set html[dir] — it only sets
   * document.documentElement.lang. RTL is applied at component level via
   * writingDirection + textAlign props. Assert via computed direction on a known
   * element and via Arabic copy presence.
   */
  test('heading computed direction is rtl on default Arabic locale', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // The heading has writingDirection={direction} which maps to CSS direction
    const headingDir = await page.getByRole('heading').last().evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(headingDir).toBe('rtl');
  });

  test('full-name textbox computed direction is rtl on default Arabic locale', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // The full-name text field has direction={direction} applied
    const nameField = page.getByTestId('register-fullname');
    const dir = await nameField.evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(dir).toBe('rtl');
  });

  test('Arabic copy is present on default locale (not raw keys)', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Default locale is Arabic — page body must contain Arabic text, not raw keys
    const pageText = await page.locator('body').innerText();
    // Heading text in AR: "إنشاء حساب" or similar — no raw i18n keys
    expect(pageText).not.toMatch(/auth\.\w+\.\w+/);
    // Page has Arabic characters
    expect(pageText).toMatch(/[؀-ۿ]/);
  });
});

test.describe('FE-TC-06 — English locale renders form LTR', () => {
  /**
   * The localeStore is NOT persisted, so the locale resets to Arabic on every
   * hard navigation. Testing LTR requires switching locale on the same page.
   * The LocaleThemeControls on /login?role=parent has testID locale-switch-en
   * (locale-switch-<loc> per LocaleThemeControls implementation). After switching
   * to English, heading computed direction should be ltr.
   *
   * NOTE: applyWebDirection() does NOT set html[dir]. RTL/LTR is at component
   * level only. We assert via heading computed direction, NOT html[dir].
   */
  test('switching to English on login makes heading LTR (component-level)', async ({ page }) => {
    await page.goto('/login?role=parent');
    await page.waitForTimeout(2000);

    // Default locale is Arabic — heading direction should be rtl
    const headingDirBefore = await page.getByRole('heading').first().evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(headingDirBefore).toBe('rtl');

    // Click the English locale switch (testID: locale-switch-en)
    const englishSwitch = page.getByTestId('locale-switch-en');
    await expect(englishSwitch).toBeVisible({ timeout: 5_000 });
    await englishSwitch.click();
    await page.waitForTimeout(500);

    // Heading computed direction should now be ltr
    const headingDirAfter = await page.getByRole('heading').first().evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(headingDirAfter).toBe('ltr');
  });
});

test.describe('FE-TC-17 — Email value stays LTR inside the RTL form', () => {
  /**
   * applyWebDirection() does NOT set html[dir]. RTL is at component level.
   * Assert RTL via heading computed direction (proxy for page-level direction).
   * Assert email field is LTR (forceValueLtr / forceLtr prop in RegisterForm).
   */
  test('email input direction is ltr even when form is in RTL locale', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Heading is RTL (Arabic locale default — component-level, not html[dir])
    const headingDir = await page.getByRole('heading').last().evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(headingDir).toBe('rtl');

    // Email input should be LTR (forceValueLtr prop in RegisterForm)
    const emailDir = await page.getByTestId('register-email').evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(emailDir).toBe('ltr');

    // Full-name textbox should be RTL (contrast)
    const nameDir = await page.getByTestId('register-fullname').evaluate(
      (el: HTMLElement) => window.getComputedStyle(el).direction,
    );
    expect(nameDir).toBe('rtl');
  });
});

// ---------------------------------------------------------------------------
// Group F — Product overrides
// ---------------------------------------------------------------------------

test.describe('FE-TC-18 — No student self-register route exists', () => {
  /**
   * Expo Router for non-existent routes does NOT redirect — the browser keeps
   * waiting for the route to resolve (JS bundle lazy-load that never completes
   * for unknown route names). We navigate with a short timeout and verify the
   * page body does NOT contain a student-register form — confirmed by checking
   * there is no Terms checkbox (parent-only feature).
   */
  test('plausible student register URLs do not mount a student register form', async ({
    page,
  }) => {
    const studentUrls = ['/register-student', '/student/register', '/signup-student'];

    for (const url of studentUrls) {
      try {
        await page.goto(url, { timeout: 4000 });
      } catch {
        // Timeout is expected for unknown routes — ignore
      }
      const currentUrl = page.url();
      // Should not end up on a student-specific register URL with a Terms checkbox
      // (The Terms checkbox via testID register-terms is unique to the parent register form)
      const termsVisible = await page.getByTestId('register-terms').isVisible().catch(() => false);
      const hasTermsOnStudentUrl = termsVisible && currentUrl.includes(url.replace('/', ''));
      // If we somehow landed on the URL AND found a Terms checkbox, that's a fail
      expect(hasTermsOnStudentUrl).toBe(false);
    }
  });
});

test.describe('FE-TC-20 — (auth) group exposes only login and parent register', () => {
  test('/register mounts the parent register form', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // Form heading visible
    await expect(page.getByRole('heading').last()).toBeVisible();
    // Password input present (register form feature)
    await expect(page.locator('input[type="password"]').first()).toBeVisible();
    // Terms checkbox present via testID (parent consent — only on parent register)
    await expect(page.getByTestId('register-terms')).toBeVisible();
    // Submit button present
    await expect(page.getByTestId('register-submit')).toBeVisible();
  });

  test('/login?role=parent mounts the login form without Terms checkbox', async ({ page }) => {
    // NOTE: /login without ?role= redirects to /role-select (Batch A). Use ?role=parent.
    await page.goto('/login?role=parent');
    await page.waitForTimeout(2000);

    // Login username field is visible
    await expect(page.getByTestId('login-username')).toBeVisible({ timeout: 10_000 });
    // Login has NO register-terms testID — it has a "Remember me" checkbox but NOT
    // the parent consent Terms checkbox. Verify register-terms is absent.
    await expect(page.getByTestId('register-terms')).not.toBeVisible();
  });

  test('register screen has sign-in link (no student-register entry point)', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.waitForTimeout(2000);

    // The sign-in link is a Text with testID "register-sign-in-link" and accessibilityRole="link"
    const signInLink = page.getByTestId('register-sign-in-link');
    await expect(signInLink).toBeVisible();

    // The link's aria-label should indicate "sign in", not student register
    const linkLabel = await signInLink.getAttribute('aria-label');
    const isSignInLink =
      (linkLabel?.includes('تسجيل الدخول') ||
        linkLabel?.toLowerCase().includes('sign in')) ??
      false;
    expect(isSignInLink).toBe(true);
  });
});
