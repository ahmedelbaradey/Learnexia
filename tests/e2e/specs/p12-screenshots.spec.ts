/**
 * p12-screenshots — Phase-1 and Phase-2 screen capture spec
 *
 * Captures every key screen in EN and AR at mobile 390px viewport,
 * saved to /tmp/p12-shots/<screen>-<locale>.png.
 *
 * Run:
 *   LD_LIBRARY_PATH="$HOME/.local/chromium-libs/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH" \
 *   npx playwright test specs/p12-screenshots.spec.ts --project=mobile --reporter=line --workers=1
 *
 * Prerequisites:
 *   - Backend running at http://localhost:5080
 *   - Expo web running at http://localhost:8081
 *
 * This spec does NOT run assertions — it only captures screenshots.
 * If a screen is not reachable it is skipped with a console note.
 */

import { test, expect, type Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// Output directory
const OUT_DIR = '/tmp/p12-shots';
const API_BASE = 'http://localhost:5080';

// Ensure output directory exists
if (!fs.existsSync(OUT_DIR)) {
  fs.mkdirSync(OUT_DIR, { recursive: true });
}

test.use({ viewport: { width: 390, height: 844 } });
test.setTimeout(300_000);

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function uniqueEmail(tag = 'shot'): string {
  return `${tag}+${Date.now()}+${Math.floor(Math.random() * 99999)}@example.com`;
}

async function shot(page: Page, name: string): Promise<void> {
  const filePath = path.join(OUT_DIR, `${name}.png`);
  await page.screenshot({ path: filePath, fullPage: false });
  console.log(`CAPTURED: ${filePath}`);
}

/**
 * Switch to English locale on the login page via the language radio.
 * Only works if called before going to a gated page (locale is overwritten by
 * useAuthRoute after login). For gated pages, we intercept /api/Users/Me.
 */
async function switchToEnglishOnLogin(page: Page): Promise<void> {
  const englishRadio = page.getByRole('radio', { name: /English/i });
  const visible = await englishRadio.isVisible({ timeout: 3_000 }).catch(() => false);
  if (visible) {
    await englishRadio.click();
    await page.waitForTimeout(500);
  }
}

/**
 * Seed a parent + child via API and return their tokens/credentials.
 */
async function seedParentAndChild(page: Page): Promise<{
  parentEmail: string;
  parentPassword: string;
  parentToken: string;
  childEmail: string;
  childPassword: string;
  childToken: string;
}> {
  const parentEmail = uniqueEmail('shotparent');
  const parentPassword = 'Str0ng!Pass1';
  const childEmail = uniqueEmail('shotchild');
  const childPassword = 'Child!Pass1';

  // Register parent
  const parentRes = await page.request.post(`${API_BASE}/api/Users/Authentication/Register-Parent`, {
    data: { email: parentEmail, password: parentPassword, fullName: 'Screenshot Parent', country: 'EG', acceptedTerms: true },
    headers: { 'Content-Type': 'application/json' },
    timeout: 30_000,
  });
  if (!parentRes.ok()) {
    throw new Error(`Register-Parent failed: ${parentRes.status()} ${await parentRes.text()}`);
  }
  const parentBody = await parentRes.json();
  const parentToken: string = parentBody.data?.accessToken ?? '';
  if (!parentToken) throw new Error('No parent accessToken');

  // Add child (grade 1, ar learning language)
  const childRes = await page.request.post(`${API_BASE}/api/Parent/Add-Child`, {
    data: {
      fullName: 'Screenshot Child',
      email: childEmail,
      password: childPassword,
      grade: 1,
      language: 'ar',
      learningLanguage: 'ar',
      country: 'EG',
    },
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${parentToken}` },
    timeout: 30_000,
  });
  if (!childRes.ok()) {
    throw new Error(`Add-Child failed: ${childRes.status()} ${await childRes.text()}`);
  }

  // Sign in as child (field is 'userName', not 'email')
  const childSignInRes = await page.request.post(`${API_BASE}/api/Users/Authentication/Sign-In`, {
    data: { userName: childEmail, password: childPassword },
    headers: { 'Content-Type': 'application/json' },
    timeout: 30_000,
  });
  if (!childSignInRes.ok()) {
    throw new Error(`Child sign-in failed: ${childSignInRes.status()} ${await childSignInRes.text()}`);
  }
  const childSignInBody = await childSignInRes.json();
  const childToken: string = childSignInBody.data?.accessToken ?? '';
  if (!childToken) throw new Error('No child accessToken');

  return { parentEmail, parentPassword, parentToken, childEmail, childPassword, childToken };
}

/**
 * Sign in as a user via the login form (UI-based sign-in, no token injection).
 */
async function signInViaUI(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.waitForTimeout(2_000);

  // Fill email
  const emailInput = page.getByTestId('login-username');
  await emailInput.fill(email);

  // Fill password
  const pwInput = page.getByTestId('login-password');
  await pwInput.fill(password);

  // Submit
  const submit = page.getByTestId('login-submit');
  await submit.click();
  await page.waitForTimeout(4_000);
}

/**
 * Sign in via API and store auth token in localStorage so the app recognises
 * the user without re-doing the login form. Also waits for the appropriate
 * route after auth.
 */
async function signInViaStorage(
  page: Page,
  token: string,
  refreshToken: string,
  role: 'parent' | 'child',
): Promise<void> {
  // Navigate to the app so we can set localStorage
  await page.goto('/');
  await page.waitForTimeout(2_000);

  // The authStore uses mmkv / AsyncStorage key 'auth-storage' in the web fallback
  // (zustand persist with AsyncStorage on web = localStorage 'auth-storage').
  // Structure: { state: { tokens: { accessToken, refreshToken }, me: null }, version: 0 }
  await page.evaluate(
    ({ accessToken, refreshToken: rt }) => {
      const authData = JSON.stringify({
        state: { tokens: { accessToken, refreshToken: rt }, me: null, isLoading: false },
        version: 0,
      });
      localStorage.setItem('auth-storage', authData);
    },
    { accessToken: token, refreshToken: refreshToken },
  );

  // Navigate to the role-appropriate home
  const target = role === 'parent' ? '/children' : '/';
  await page.goto(target);
  await page.waitForTimeout(3_000);
}

/**
 * Intercept GET /api/Users/Me and inject preferredLanguage='en' into the response
 * so the app renders in English (locale is set from Me.preferredLanguage after login).
 */
async function interceptMeForEnglish(page: Page): Promise<void> {
  await page.route('**/api/Users/Me**', async (route) => {
    const response = await route.fetch();
    const json = await response.json();
    if (json?.data?.preferredLanguage !== undefined) {
      json.data.preferredLanguage = 'en';
    } else if (json?.data) {
      json.data.preferredLanguage = 'en';
    }
    await route.fulfill({ response, json });
  });
}

// ---------------------------------------------------------------------------
// Shared seed (one parent+child per suite run)
// ---------------------------------------------------------------------------

let SEED: Awaited<ReturnType<typeof seedParentAndChild>> | null = null;
let SEED_ERROR: string | null = null;

/**
 * Run seed once before the very first test that needs it.
 * We use a lazy-init pattern: each seeded describe calls ensureSeed in its beforeAll.
 */
async function ensureSeed(browser: import('@playwright/test').Browser): Promise<void> {
  if (SEED || SEED_ERROR) return;
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  try {
    SEED = await seedParentAndChild(page);
  } catch (e) {
    SEED_ERROR = String(e);
    console.log('Global seed failed:', e);
  } finally {
    await ctx.close();
  }
}

// ---------------------------------------------------------------------------
// SPLASH  (/  before auth resolved — just the loading flash)
// ---------------------------------------------------------------------------

test.describe('splash', () => {
  test('splash-ar: capture / on first load', async ({ page }) => {
    // Navigate to root — the splash or auth redirect is what we see momentarily
    await page.goto('/');
    // Give the app 1 second to render the splash (before redirect kicks in)
    await page.waitForTimeout(1_500);
    await shot(page, 'splash-ar');
  });

  test('splash-en: capture / in English', async ({ page }) => {
    // Use login page locale switch to get English state visible
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    // Then navigate to splash — Expo Router will redirect immediately, capture fast
    // Actually, just go to / and catch whatever renders
    await page.goto('/');
    await page.waitForTimeout(1_000);
    await shot(page, 'splash-en');
  });
});

// ---------------------------------------------------------------------------
// LOGIN  (/login)
// ---------------------------------------------------------------------------

test.describe('login', () => {
  test('login-ar: login screen in Arabic (default)', async ({ page }) => {
    await page.goto('/login');
    await page.waitForTimeout(2_500);
    await shot(page, 'login-ar');
  });

  test('login-en: login screen in English', async ({ page }) => {
    await page.goto('/login');
    await page.waitForTimeout(2_500);
    await switchToEnglishOnLogin(page);
    await page.waitForTimeout(500);
    await shot(page, 'login-en');
  });
});

// ---------------------------------------------------------------------------
// REGISTER  (/register)
// ---------------------------------------------------------------------------

test.describe('register', () => {
  test('register-ar: register screen in Arabic (default)', async ({ page }) => {
    await page.goto('/register');
    await page.waitForTimeout(2_500);
    await shot(page, 'register-ar');
  });

  test('register-en: register screen in English', async ({ page }) => {
    // Switch locale on login first, then navigate
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    await page.goto('/register');
    await page.waitForTimeout(2_500);
    await shot(page, 'register-en');
  });
});

// ---------------------------------------------------------------------------
// ADD-CHILD ONBOARDING  (/add-child)
// ---------------------------------------------------------------------------

test.describe('add-child', () => {
  let parentEmail = '';
  let parentPassword = '';

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    try {
      parentEmail = uniqueEmail('addchildparent');
      parentPassword = 'Str0ng!Pass1';
      const res = await page.request.post(`${API_BASE}/api/Users/Authentication/Register-Parent`, {
        data: { email: parentEmail, password: parentPassword, fullName: 'AddChild Parent', country: 'EG', acceptedTerms: true },
        headers: { 'Content-Type': 'application/json' },
        timeout: 30_000,
      });
      if (!res.ok()) {
        parentEmail = '';
        console.log('add-child seed failed:', await res.text());
      }
    } catch (e) {
      parentEmail = '';
      console.log('add-child seed error:', e);
    } finally {
      await ctx.close();
    }
  });

  test('add-child-ar: add-child onboarding form in Arabic', async ({ page }) => {
    if (!parentEmail) {
      console.log('SKIP add-child-ar: parent seed failed');
      return;
    }
    await signInViaUI(page, parentEmail, parentPassword);
    // After sign-in as parent without children, should land on /add-child
    await page.waitForURL(/add-child|onboarding/, { timeout: 20_000 }).catch(() => {});
    await page.waitForTimeout(2_000);
    await shot(page, 'add-child-ar');
  });

  test('add-child-en: add-child onboarding form in English', async ({ page }) => {
    if (!parentEmail) {
      console.log('SKIP add-child-en: parent seed failed');
      return;
    }
    // Intercept Me to inject English locale before login completes
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    // Switch locale UI toggle too
    await switchToEnglishOnLogin(page);

    // Fill login form with same parent (still has no children → routes to add-child)
    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(parentEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(parentPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();

    await page.waitForURL(/add-child|onboarding/, { timeout: 20_000 }).catch(() => {});
    await page.waitForTimeout(2_000);
    await shot(page, 'add-child-en');
  });
});

// ---------------------------------------------------------------------------
// HOME / CHILD DASHBOARD  (/(child)/)
// ---------------------------------------------------------------------------

test.describe('home', () => {
  test.beforeAll(async ({ browser }) => { await ensureSeed(browser); });

  test('home-ar: child dashboard in Arabic', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP home-ar: seed failed:', SEED_ERROR);
      return;
    }
    await signInViaUI(page, SEED.childEmail, SEED.childPassword);
    await page.waitForTimeout(3_000);
    // Should be on child home
    await shot(page, 'home-ar');
  });

  test('home-en: child dashboard in English', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP home-en: seed failed:', SEED_ERROR);
      return;
    }
    // Intercept Me to inject English preference
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);

    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(SEED.childEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(SEED.childPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();
    await page.waitForTimeout(4_000);
    await shot(page, 'home-en');
  });
});

// ---------------------------------------------------------------------------
// SUBJECT LESSONS LIST  (/(child)/subjects/[subjectId])
// ---------------------------------------------------------------------------

test.describe('subject-lessons', () => {
  test.beforeAll(async ({ browser }) => { await ensureSeed(browser); });

  test('subject-lessons-ar: lessons list for subject 1 in Arabic', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP subject-lessons-ar: no seed');
      return;
    }
    await signInViaUI(page, SEED.childEmail, SEED.childPassword);
    await page.waitForTimeout(2_000);
    await page.goto('/(child)/subjects/1');
    await page.waitForTimeout(3_000);
    await shot(page, 'subject-lessons-ar');
  });

  test('subject-lessons-en: lessons list in English', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP subject-lessons-en: no seed');
      return;
    }
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(SEED.childEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(SEED.childPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();
    await page.waitForTimeout(3_000);
    await page.goto('/(child)/subjects/1');
    await page.waitForTimeout(3_000);
    await shot(page, 'subject-lessons-en');
  });
});

// ---------------------------------------------------------------------------
// SKILL TREE  (/(child)/subjects/[subjectId]/tree)
// ---------------------------------------------------------------------------

test.describe('skill-tree', () => {
  test.beforeAll(async ({ browser }) => { await ensureSeed(browser); });

  test('skill-tree-ar: skill tree in Arabic', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP skill-tree-ar: no seed');
      return;
    }
    await signInViaUI(page, SEED.childEmail, SEED.childPassword);
    await page.waitForTimeout(2_000);
    await page.goto('/(child)/subjects/1/tree');
    await page.waitForTimeout(3_000);
    await shot(page, 'skill-tree-ar');
  });

  test('skill-tree-en: skill tree in English', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP skill-tree-en: no seed');
      return;
    }
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(SEED.childEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(SEED.childPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();
    await page.waitForTimeout(3_000);
    await page.goto('/(child)/subjects/1/tree');
    await page.waitForTimeout(3_000);
    await shot(page, 'skill-tree-en');
  });
});

// ---------------------------------------------------------------------------
// LESSON INTRO  (/(child)/lessons/1?subjectId=1 — intro stage)
// ---------------------------------------------------------------------------

test.describe('lesson-intro', () => {
  test.beforeAll(async ({ browser }) => { await ensureSeed(browser); });

  test('lesson-intro-ar: lesson intro card in Arabic', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP lesson-intro-ar: no seed');
      return;
    }
    await signInViaUI(page, SEED.childEmail, SEED.childPassword);
    await page.waitForTimeout(2_000);
    await page.goto('/(child)/lessons/1?subjectId=1');
    await page.waitForTimeout(3_000);
    // The intro stage renders the lesson-intro card before start is clicked
    await shot(page, 'lesson-intro-ar');
  });

  test('lesson-intro-en: lesson intro card in English', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP lesson-intro-en: no seed');
      return;
    }
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(SEED.childEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(SEED.childPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();
    await page.waitForTimeout(3_000);
    await page.goto('/(child)/lessons/1?subjectId=1');
    await page.waitForTimeout(3_000);
    await shot(page, 'lesson-intro-en');
  });
});

// ---------------------------------------------------------------------------
// QUIZ  (/(child)/lessons/1?subjectId=1 — quiz stage, one question)
// ---------------------------------------------------------------------------

test.describe('quiz', () => {
  test.beforeAll(async ({ browser }) => { await ensureSeed(browser); });

  test('quiz-ar: quiz question with instant feedback in Arabic', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP quiz-ar: no seed');
      return;
    }
    await signInViaUI(page, SEED.childEmail, SEED.childPassword);
    await page.waitForTimeout(2_000);
    await page.goto('/(child)/lessons/1?subjectId=1');
    await page.waitForTimeout(3_000);

    // Click Start to enter quiz stage
    const startCta = page.getByTestId('lesson-start-cta');
    const startVisible = await startCta.isVisible({ timeout: 5_000 }).catch(() => false);
    if (startVisible) {
      await startCta.click();
      await page.waitForTimeout(2_000);
    }
    // Capture the quiz question
    await shot(page, 'quiz-ar');

    // Try to select first MCQ option and submit to show instant feedback
    const option0 = page.getByTestId('quiz-mcq-option-0');
    const optionVisible = await option0.isVisible({ timeout: 3_000 }).catch(() => false);
    if (optionVisible) {
      await option0.click();
      await page.waitForTimeout(500);
      const submitBtn = page.getByTestId('quiz-submit');
      const submitVisible = await submitBtn.isVisible({ timeout: 3_000 }).catch(() => false);
      if (submitVisible) {
        await submitBtn.click();
        await page.waitForTimeout(2_000);
        // Capture the feedback state
        await shot(page, 'quiz-feedback-ar');
      }
    }
  });

  test('quiz-en: quiz question in English', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP quiz-en: no seed');
      return;
    }
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(SEED.childEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(SEED.childPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();
    await page.waitForTimeout(3_000);
    await page.goto('/(child)/lessons/1?subjectId=1');
    await page.waitForTimeout(3_000);

    const startCta = page.getByTestId('lesson-start-cta');
    const startVisible = await startCta.isVisible({ timeout: 5_000 }).catch(() => false);
    if (startVisible) {
      await startCta.click();
      await page.waitForTimeout(2_000);
    }
    await shot(page, 'quiz-en');

    const option0 = page.getByTestId('quiz-mcq-option-0');
    const optionVisible = await option0.isVisible({ timeout: 3_000 }).catch(() => false);
    if (optionVisible) {
      await option0.click();
      await page.waitForTimeout(500);
      const submitBtn = page.getByTestId('quiz-submit');
      const submitVisible = await submitBtn.isVisible({ timeout: 3_000 }).catch(() => false);
      if (submitVisible) {
        await submitBtn.click();
        await page.waitForTimeout(2_000);
        await shot(page, 'quiz-feedback-en');
      }
    }
  });
});

// ---------------------------------------------------------------------------
// REWARD / SUMMARY  (lesson complete → AttemptSummaryCard)
// ---------------------------------------------------------------------------

test.describe('reward', () => {
  test.beforeAll(async ({ browser }) => { await ensureSeed(browser); });

  /**
   * The summary card appears after all quiz questions are answered.
   * We mock the grading + complete endpoints so we can reach it headlessly.
   */
  test('reward-ar: attempt summary / reward screen in Arabic', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP reward-ar: no seed');
      return;
    }
    await signInViaUI(page, SEED.childEmail, SEED.childPassword);
    await page.waitForTimeout(2_000);

    // Mock grading to return isCorrect=false (feedback path, no celebration)
    await page.route('**/Quizzes/*/Answers', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          successed: true, errors: [],
          data: { isCorrect: false, correctAnswer: '6', hintAvailable: false },
        }),
      }),
    );
    // Mock complete to return a summary
    await page.route('**/Quizzes/*/Complete', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          successed: true, errors: [],
          data: {
            totalQuestions: 1, correctAnswers: 0,
            score: 0, xpEarned: 5, heartsLost: 1,
            badges: [], streakMilestone: false,
          },
        }),
      }),
    );

    await page.goto('/(child)/lessons/1?subjectId=1');
    await page.waitForTimeout(3_000);

    const startCta = page.getByTestId('lesson-start-cta');
    const startVisible = await startCta.isVisible({ timeout: 5_000 }).catch(() => false);
    if (startVisible) {
      await startCta.click();
      await page.waitForTimeout(2_000);
    }

    // Answer each question to trigger complete
    const option0 = page.getByTestId('quiz-mcq-option-0');
    const optionVisible = await option0.isVisible({ timeout: 5_000 }).catch(() => false);
    if (!optionVisible) {
      console.log('SKIP reward-ar: no MCQ option visible');
      return;
    }

    // Go through all questions
    let attempts = 0;
    while (attempts < 10) {
      const q = page.getByTestId('quiz-question-card');
      const qVisible = await q.isVisible({ timeout: 3_000 }).catch(() => false);
      if (!qVisible) break;

      const o0 = page.getByTestId('quiz-mcq-option-0');
      const o0v = await o0.isVisible({ timeout: 2_000 }).catch(() => false);
      if (o0v) await o0.click();

      const submitBtn = page.getByTestId('quiz-submit');
      const submitV = await submitBtn.isVisible({ timeout: 2_000 }).catch(() => false);
      if (submitV) {
        await submitBtn.click();
        await page.waitForTimeout(1_500);
      }

      // Click continue/next after feedback
      const cont = page.getByTestId('feedback-continue');
      const contV = await cont.isVisible({ timeout: 2_000 }).catch(() => false);
      if (contV) {
        await cont.click();
        await page.waitForTimeout(1_500);
      }

      attempts++;
    }

    // Wait for summary
    const summary = page.getByTestId('lesson-summary');
    const summaryVisible = await summary.isVisible({ timeout: 8_000 }).catch(() => false);
    if (summaryVisible) {
      await shot(page, 'reward-ar');
    } else {
      // Capture whatever state we're in
      await shot(page, 'reward-ar');
      console.log('reward-ar: summary may not be visible — captured current state');
    }
  });

  test('reward-en: attempt summary / reward screen in English', async ({ page }) => {
    if (!SEED) {
      console.log('SKIP reward-en: no seed');
      return;
    }
    await interceptMeForEnglish(page);
    await page.goto('/login');
    await page.waitForTimeout(2_000);
    await switchToEnglishOnLogin(page);
    const emailInput = page.getByTestId('login-username');
    await emailInput.fill(SEED.childEmail);
    const pwInput = page.getByTestId('login-password');
    await pwInput.fill(SEED.childPassword);
    const submit = page.getByTestId('login-submit');
    await submit.click();
    await page.waitForTimeout(3_000);

    await page.route('**/Quizzes/*/Answers', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          successed: true, errors: [],
          data: { isCorrect: false, correctAnswer: '6', hintAvailable: false },
        }),
      }),
    );
    await page.route('**/Quizzes/*/Complete', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          successed: true, errors: [],
          data: {
            totalQuestions: 1, correctAnswers: 0,
            score: 0, xpEarned: 5, heartsLost: 1,
            badges: [], streakMilestone: false,
          },
        }),
      }),
    );

    await page.goto('/(child)/lessons/1?subjectId=1');
    await page.waitForTimeout(3_000);

    const startCta = page.getByTestId('lesson-start-cta');
    const startVisible = await startCta.isVisible({ timeout: 5_000 }).catch(() => false);
    if (startVisible) {
      await startCta.click();
      await page.waitForTimeout(2_000);
    }

    let attempts = 0;
    while (attempts < 10) {
      const q = page.getByTestId('quiz-question-card');
      const qVisible = await q.isVisible({ timeout: 3_000 }).catch(() => false);
      if (!qVisible) break;

      const o0 = page.getByTestId('quiz-mcq-option-0');
      const o0v = await o0.isVisible({ timeout: 2_000 }).catch(() => false);
      if (o0v) await o0.click();

      const submitBtn = page.getByTestId('quiz-submit');
      const submitV = await submitBtn.isVisible({ timeout: 2_000 }).catch(() => false);
      if (submitV) {
        await submitBtn.click();
        await page.waitForTimeout(1_500);
      }

      const cont = page.getByTestId('feedback-continue');
      const contV = await cont.isVisible({ timeout: 2_000 }).catch(() => false);
      if (contV) {
        await cont.click();
        await page.waitForTimeout(1_500);
      }
      attempts++;
    }

    const summary = page.getByTestId('lesson-summary');
    const summaryVisible = await summary.isVisible({ timeout: 8_000 }).catch(() => false);
    if (summaryVisible) {
      await shot(page, 'reward-en');
    } else {
      await shot(page, 'reward-en');
      console.log('reward-en: summary may not be visible — captured current state');
    }
  });
});
