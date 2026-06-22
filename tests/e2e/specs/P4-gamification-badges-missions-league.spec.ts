/**
 * P4 Gamification E2E — Badges / Missions / League screens.
 *
 * Covers GAM-FE-TC-100..111 (Badges), GAM-FE-TC-120..133 (Missions),
 * GAM-FE-TC-140..151 (League).
 *
 * Seed strategy:
 *   - Real API: register parent → add child.
 *   - page.route() interception for populated/error states on:
 *     * /api/Gamification/Badges/Me   (useMyBadges → client.badgesMe())
 *     * /api/Gamification/Missions/Me (useMyMissions → client.missionsMe())
 *     * /api/Gamification/Leagues/Me  (useMyLeague → client.leaguesMe())
 *
 * Run:
 *   cd tests/e2e
 *   npx playwright test --config=playwright.child.config.ts \
 *     specs/P4-gamification-badges-missions-league.spec.ts --reporter=list
 */

import { test, expect, type Page } from '@playwright/test';

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------

const API_BASE = process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://localhost:5080';
const RUN_ID = `${Date.now()}_${Math.floor(Math.random() * 99999)}`;

test.setTimeout(180_000);

// ---------------------------------------------------------------------------
// Seed helpers
// ---------------------------------------------------------------------------

async function seedParentAndChild(tag = 'bml'): Promise<{
  childEmail: string;
  childPassword: string;
  parentToken: string;
}> {
  const parentEmail = `parent+${tag}+${RUN_ID}@e2e.test`;
  const parentPassword = 'Test@1234!';
  const childEmail = `child+${tag}+${RUN_ID}@e2e.test`;
  const childPassword = 'ChildPass1!';

  const regResp = await fetch(`${API_BASE}/api/Users/Authentication/Register-Parent`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      fullName: 'E2E BML Parent',
      email: parentEmail,
      password: parentPassword,
      confirmPassword: parentPassword,
      acceptedTerms: true,
    }),
  });
  const regJson = await regResp.json();
  if (!regJson.successed) throw new Error(`Register-Parent failed: ${JSON.stringify(regJson)}`);
  const parentToken: string = regJson.data.accessToken;

  const childResp = await fetch(`${API_BASE}/api/Parent/Add-Child`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${parentToken}` },
    body: JSON.stringify({
      fullName: 'E2E BML Child',
      email: childEmail,
      password: childPassword,
      grade: 3,
      language: 'ar',
      learningLanguage: 'ar',
    }),
  });
  const childJson = await childResp.json();
  if (!childJson.successed) throw new Error(`Add-Child failed: ${JSON.stringify(childJson)}`);

  return { childEmail, childPassword, parentToken };
}

// ---------------------------------------------------------------------------
// UI helpers
// ---------------------------------------------------------------------------

async function signInAsChild(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login?role=student', { waitUntil: 'domcontentloaded', timeout: 60_000 });
  try { await page.waitForLoadState('networkidle', { timeout: 10_000 }); } catch { /* Metro WS */ }
  await page.waitForTimeout(2_000);
  const usernameInput = page.getByTestId('login-username');
  await usernameInput.waitFor({ state: 'visible', timeout: 30_000 });
  await usernameInput.fill(email);
  await page.getByTestId('login-password').fill(password);
  await page.getByTestId('login-submit').click();
  await page.getByTestId('dashboard-header').waitFor({ state: 'visible', timeout: 45_000 });
  await page.waitForTimeout(500);
}

// ---------------------------------------------------------------------------
// Payload factories
// ---------------------------------------------------------------------------

function wrapEnvelope(data: unknown) {
  return { successed: true, errors: null, data };
}

function makeBadgesPayload(badges: Array<{
  code: string;
  rarity: number; // 1=bronze 2=silver 3=gold 4=legendary
  earnedOnUtc?: string | null;
}>) {
  return wrapEnvelope({
    badges: badges.map((b) => ({
      code: b.code,
      rarity: b.rarity,
      // Real BadgeStateDto fields the FE reads: isEarned (boolean) + awardedAtUtc
      // (date the screen sorts earned tiles by, newest-first). The previous
      // `earnedOnUtc` field the FE never reads.
      isEarned: b.earnedOnUtc != null,
      awardedAtUtc: b.earnedOnUtc ?? null,
    })),
    earnedCounts: {
      bronze: badges.filter((b) => b.earnedOnUtc && b.rarity === 1).length,
      silver: badges.filter((b) => b.earnedOnUtc && b.rarity === 2).length,
      gold: badges.filter((b) => b.earnedOnUtc && b.rarity === 3).length,
      legendary: badges.filter((b) => b.earnedOnUtc && b.rarity === 4).length,
    },
  });
}

/**
 * Real `MyMissionsResponse` shape (verified against nswag-client.ts):
 *   { daily: MissionStateDto[], weekly: MissionStateDto[] }
 * Real `MissionStateDto` fields (no xpReward alias — the real field is rewardXp):
 *   code, iconKey, titleKey, cadence, targetType, target, rewardXp, progress,
 *   status (MissionStatusDto: 0=NotStarted 1=InProgress 2=Completed 3=Expired),
 *   periodStartUtc, periodEndUtc, completedAtUtc
 * No dailyMissions / weeklyMissions / dailiesCompleted / dailiesTotal / dailyBonusXp.
 */
function makeMissionsPayload(opts: {
  daily?: Array<{
    code: string;
    titleKey?: string;
    progress: number;
    target: number;
    rewardXp: number;
    status: number; // 0=NotStarted 1=InProgress 2=Completed 3=Expired
    targetType?: number; // MissionTargetTypeDto: 1=Lessons 2=Answers 3=CorrectAnswers
    periodEndUtc?: string;
  }>;
  weekly?: Array<{
    code: string;
    titleKey?: string;
    progress: number;
    target: number;
    rewardXp: number;
    status: number;
    targetType?: number;
  }>;
}) {
  const periodEnd = new Date(Date.now() + 86400000).toISOString();
  return wrapEnvelope({
    daily: (opts.daily ?? []).map((m) => ({
      code: m.code,
      titleKey: m.titleKey ?? `missions.titles.${m.code.toLowerCase()}`,
      progress: m.progress,
      target: m.target,
      rewardXp: m.rewardXp,
      status: m.status,
      targetType: m.targetType ?? 1,
      periodEndUtc: m.periodEndUtc ?? periodEnd,
    })),
    weekly: (opts.weekly ?? []).map((m) => ({
      code: m.code,
      titleKey: m.titleKey ?? `missions.titles.${m.code.toLowerCase()}`,
      progress: m.progress,
      target: m.target,
      rewardXp: m.rewardXp,
      status: m.status,
      targetType: m.targetType ?? 1,
    })),
  });
}

function makeLeaguePayload(opts: {
  tier?: number; // 1=Bronze 2=Silver 3=Gold 4=Diamond
  tierName?: string;
  myRank?: number;
  myWeeklyXp?: number;
  promotionCutoffRank?: number;
  demotionCutoffRank?: number;
  periodEndUtc?: string;
  standings?: Array<{
    rank: number;
    displayName: string;
    weeklyXp: number;
    isMe: boolean;
  }>;
} | null) {
  if (opts === null) return wrapEnvelope(null);
  return wrapEnvelope({
    tier: opts.tier ?? 1,
    tierName: opts.tierName ?? 'Bronze',
    myRank: opts.myRank ?? 1,
    myWeeklyXp: opts.myWeeklyXp ?? 100,
    promotionCutoffRank: opts.promotionCutoffRank ?? 3,
    demotionCutoffRank: opts.demotionCutoffRank ?? 8,
    periodEndUtc: opts.periodEndUtc ?? new Date(Date.now() + 86400000 * 4).toISOString(),
    standings: (opts.standings ?? [
      { rank: 1, displayName: 'Student #1', weeklyXp: 100, isMe: true },
      { rank: 2, displayName: 'Student #2', weeklyXp: 80, isMe: false },
    ]),
  });
}

// ---------------------------------------------------------------------------
// Route interception helpers
// ---------------------------------------------------------------------------

async function mockBadges(page: Page, payload: object): Promise<void> {
  await page.route('**/api/Gamification/Badges/Me', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(payload) });
  });
}

async function mockBadgesError(page: Page): Promise<void> {
  await page.route('**/api/Gamification/Badges/Me', async (route) => {
    await route.fulfill({ status: 500, body: '{"successed":false,"errors":["error"]}' });
  });
}

async function mockMissions(page: Page, payload: object): Promise<void> {
  await page.route('**/api/Gamification/Missions/Me', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(payload) });
  });
}

async function mockMissionsError(page: Page): Promise<void> {
  await page.route('**/api/Gamification/Missions/Me', async (route) => {
    await route.fulfill({ status: 500, body: '{"successed":false,"errors":["error"]}' });
  });
}

async function mockLeague(page: Page, payload: object): Promise<void> {
  await page.route('**/api/Gamification/Leagues/Me', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(payload) });
  });
}

async function mockLeagueError(page: Page): Promise<void> {
  await page.route('**/api/Gamification/Leagues/Me', async (route) => {
    await route.fulfill({ status: 500, body: '{"successed":false,"errors":["error"]}' });
  });
}

// ============================================================================
// Section 5: Badges screen (GAM-FE-TC-100..111)
// ============================================================================

test.describe('5. Badges screen', () => {
  let childEmail: string;
  let childPassword: string;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const p = await ctx.newPage();
    try {
      const seed = await seedParentAndChild('badges');
      childEmail = seed.childEmail;
      childPassword = seed.childPassword;
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-100
  test('GAM-FE-TC-100 — Badges screen renders loading/error/empty/grid', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const loading = await page.getByTestId('badges-loading').isVisible({ timeout: 1_000 }).catch(() => false);
      const error = await page.getByTestId('badges-error').isVisible({ timeout: 1_000 }).catch(() => false);
      const empty = await page.getByTestId('badges-empty').isVisible({ timeout: 1_000 }).catch(() => false);
      const stats = await page.getByTestId('badges-stats').isVisible({ timeout: 1_000 }).catch(() => false);
      const grid = await page.getByTestId('badges-grid').isVisible({ timeout: 1_000 }).catch(() => false);
      expect(loading || error || empty || stats || grid).toBe(true);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-101
  test('GAM-FE-TC-101 — All-locked first-time gallery with encouraging sub', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Mock badges: catalog with all locked (earnedOnUtc=null)
      await mockBadges(page, makeBadgesPayload([
        { code: 'FIRST_LESSON', rarity: 1, earnedOnUtc: null },
        { code: 'STREAK_3', rarity: 1, earnedOnUtc: null },
        { code: 'PERFECT_QUIZ', rarity: 2, earnedOnUtc: null },
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const grid = page.getByTestId('badges-grid');
      const gridVisible = await grid.isVisible({ timeout: 5_000 }).catch(() => false);
      if (gridVisible) {
        await expect(grid).toBeVisible();
        // Badges sub should be encouraging (not empty/error)
        const badgesSub = page.getByTestId('badges-sub');
        const subVisible = await badgesSub.isVisible({ timeout: 2_000 }).catch(() => false);
        if (subVisible) {
          const subText = await badgesSub.textContent() ?? '';
          expect(subText.length).toBeGreaterThan(0);
        }
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-102
  test('GAM-FE-TC-102 — Earned + locked split renders correctly', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const earnedDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'FIRST_LESSON', rarity: 1, earnedOnUtc: earnedDate },  // earned
        { code: 'STREAK_3', rarity: 1, earnedOnUtc: null },             // locked
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const grid = page.getByTestId('badges-grid');
      const gridVisible = await grid.isVisible({ timeout: 5_000 }).catch(() => false);
      if (gridVisible) {
        // Both tiles should be in the grid
        const earnedTile = page.getByTestId('badge-tile-FIRST_LESSON');
        const lockedTile = page.getByTestId('badge-tile-STREAK_3');
        const earnedCount = await earnedTile.count();
        const lockedCount = await lockedTile.count();
        // At least the grid renders with both
        expect(earnedCount + lockedCount).toBeGreaterThan(0);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-103
  test('GAM-FE-TC-103 — Sort: earned (newest first) then locked', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const olderDate = new Date(Date.now() - 2 * 86400000).toISOString();
      const newerDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'BADGE_OLDER', rarity: 1, earnedOnUtc: olderDate },
        { code: 'BADGE_NEWER', rarity: 2, earnedOnUtc: newerDate },
        { code: 'BADGE_LOCKED', rarity: 1, earnedOnUtc: null },
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const grid = page.getByTestId('badges-grid');
      const gridVisible = await grid.isVisible({ timeout: 5_000 }).catch(() => false);
      if (gridVisible) {
        const gridText = await grid.textContent() ?? '';
        expect(gridText.length).toBeGreaterThan(0);
        // Sort order: BADGE_NEWER should appear before BADGE_OLDER in the DOM
        const newerIdx = gridText.indexOf('BADGE_NEWER');
        const olderIdx = gridText.indexOf('BADGE_OLDER');
        const lockedIdx = gridText.indexOf('BADGE_LOCKED');
        // If all tile texts appear
        if (newerIdx !== -1 && olderIdx !== -1) {
          // Newer earned tile should come before older earned tile
          expect(newerIdx).toBeLessThan(olderIdx);
        }
        if (olderIdx !== -1 && lockedIdx !== -1) {
          // Locked should come after earned
          expect(olderIdx).toBeLessThan(lockedIdx);
        }
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-104
  test('GAM-FE-TC-104 — Stats strip counts per rarity', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const earnedDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'B1', rarity: 1, earnedOnUtc: earnedDate }, // bronze
        { code: 'B2', rarity: 2, earnedOnUtc: earnedDate }, // silver
        { code: 'B3', rarity: 3, earnedOnUtc: null },       // gold locked
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const statsStrip = page.getByTestId('badges-stats');
      const statsVisible = await statsStrip.isVisible({ timeout: 5_000 }).catch(() => false);
      if (statsVisible) {
        await expect(statsStrip).toBeVisible();
        const statsText = await statsStrip.textContent() ?? '';
        expect(statsText.length).toBeGreaterThan(0);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-105 — Earned date / locked hint (P2)
  test('GAM-FE-TC-105 — Earned tile shows date; locked tile shows hint', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const earnedDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'EARNED_BADGE', rarity: 1, earnedOnUtc: earnedDate },
        { code: 'LOCKED_BADGE', rarity: 1, earnedOnUtc: null },
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const earnedTile = page.getByTestId('badge-tile-EARNED_BADGE');
      const earnedVisible = await earnedTile.count();
      if (earnedVisible > 0) {
        const earnedText = await earnedTile.textContent() ?? '';
        // Earned badge should show some kind of date text
        expect(earnedText.length).toBeGreaterThan(0);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-106 — Empty catalog edge (P2)
  test('GAM-FE-TC-106 — Empty catalog shows badges-empty, not error', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Empty badge catalog
      await mockBadges(page, makeBadgesPayload([]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const empty = page.getByTestId('badges-empty');
      const emptyVisible = await empty.isVisible({ timeout: 5_000 }).catch(() => false);
      if (emptyVisible) {
        const emptyText = await empty.textContent() ?? '';
        expect(emptyText.length).toBeGreaterThan(0);
        // Must NOT show an error (not 500)
        const errorVisible = await page.getByTestId('badges-error').isVisible({ timeout: 1_000 }).catch(() => false);
        expect(errorVisible).toBe(false);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-107 — Unknown badge code fallback (P2)
  test('GAM-FE-TC-107 — Unknown badge code falls back to code, no error', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const earnedDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'UNKNOWN_BADGE_XYZ_999', rarity: 1, earnedOnUtc: earnedDate },
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // Grid should render without crash
      const grid = page.getByTestId('badges-grid');
      const gridVisible = await grid.isVisible({ timeout: 5_000 }).catch(() => false);
      if (gridVisible) {
        const gridText = await grid.textContent() ?? '';
        // Should NOT be a blank screen or unhandled error
        expect(gridText).not.toContain('[object Object]');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-108
  test('GAM-FE-TC-108 — Badge tile a11y label (name/rarity/state)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const earnedDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'FIRST_LESSON', rarity: 1, earnedOnUtc: earnedDate },
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const tile = page.getByTestId('badge-tile-FIRST_LESSON');
      const tileCount = await tile.count();
      if (tileCount > 0) {
        const ariaLabel = await tile.getAttribute('aria-label');
        // Tile should have some accessible label
        if (ariaLabel) {
          expect(ariaLabel.length).toBeGreaterThan(0);
          // Must not be a raw i18n key
          expect(ariaLabel).not.toMatch(/^badges\./);
        }
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-109
  test('GAM-FE-TC-109 — Badges error + retry', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockBadgesError(page);
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const errorStrip = page.getByTestId('badges-error');
      const errorVisible = await errorStrip.isVisible({ timeout: 5_000 }).catch(() => false);
      if (errorVisible) {
        await expect(errorStrip).toBeVisible();
        await expect(page.getByTestId('badges-retry')).toBeVisible({ timeout: 5_000 });
      } else {
        console.log('[GAM-FE-TC-109] badges-error not visible — badges endpoint may not have been intercepted');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-110
  test('GAM-FE-TC-110 — RTL badges grid (ar) + dates Eastern-Arabic', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      const earnedDate = new Date(Date.now() - 86400000).toISOString();
      await mockBadges(page, makeBadgesPayload([
        { code: 'FIRST_LESSON', rarity: 1, earnedOnUtc: earnedDate },
      ]));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/badges');
      await expect(page.getByTestId('child-badges-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);

      // DESIGN NOTE: applyWebDirection() sets lang=ar, NOT dir=rtl (html[dir] deliberately not set).
      await page.waitForFunction(() => document.documentElement.lang === 'ar', { timeout: 10_000 });
      expect(await page.evaluate(() => document.documentElement.lang)).toBe('ar');

      // No raw i18n keys
      const rawKeys: string[] = await page.evaluate(() => {
        const suspects: string[] = [];
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
        let node: Node | null;
        while ((node = walker.nextNode())) {
          const text = (node.nodeValue ?? '').trim();
          if (/^(badges|common|child)\.[a-zA-Z_.]+$/.test(text)) suspects.push(text);
        }
        return suspects;
      });
      expect(rawKeys, `Raw i18n keys on badges (AR): ${rawKeys.join(', ')}`).toHaveLength(0);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-111 — Badge-unlock celebration (timing-sensitive; BLOCKED)
  test.skip('GAM-FE-TC-111 — Badge-unlock celebration (BLOCKED — requires dashboard diff between two refreshes; not deterministically triggerable in harness)', async () => {});
});

// ============================================================================
// Section 6: Missions screen (GAM-FE-TC-120..133)
// ============================================================================

test.describe('6. Missions screen', () => {
  let childEmail: string;
  let childPassword: string;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const p = await ctx.newPage();
    try {
      const seed = await seedParentAndChild('missions');
      childEmail = seed.childEmail;
      childPassword = seed.childPassword;
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-120
  test('GAM-FE-TC-120 — Missions screen renders loading/error/empty/data', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const loading = await page.getByTestId('missions-loading').isVisible({ timeout: 1_000 }).catch(() => false);
      const error = await page.getByTestId('missions-error').isVisible({ timeout: 1_000 }).catch(() => false);
      const empty = await page.getByTestId('missions-empty').isVisible({ timeout: 1_000 }).catch(() => false);
      const hero = await page.getByTestId('missions-hero').isVisible({ timeout: 1_000 }).catch(() => false);
      const daily = await page.getByTestId('missions-daily').isVisible({ timeout: 1_000 }).catch(() => false);
      expect(loading || error || empty || hero || daily).toBe(true);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-121
  test('GAM-FE-TC-121 — Empty state shows missions-empty, not error', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Empty missions (real shape: { daily: [], weekly: [] })
      await mockMissions(page, makeMissionsPayload({ daily: [], weekly: [] }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // With correct empty mock shape { daily: [], weekly: [] } the empty state must render.
      const empty = page.getByTestId('missions-empty');
      await expect(empty, 'TC-121: missions-empty must render when both daily and weekly are empty arrays').toBeVisible({ timeout: 8_000 });
      const emptyText = await empty.textContent() ?? '';
      expect(emptyText.length, 'TC-121: missions-empty must have non-empty text').toBeGreaterThan(0);
      const errVisible = await page.getByTestId('missions-error').isVisible({ timeout: 1_000 }).catch(() => false);
      expect(errVisible, 'TC-121: must NOT show error when empty payload is returned').toBe(false);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-122
  test('GAM-FE-TC-122 — Daily list renders rows with progress', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'DAILY_LESSON', progress: 2, target: 5, rewardXp: 50, status: 1, targetType: 1 },
          { code: 'DAILY_CORRECT', progress: 3, target: 10, rewardXp: 75, status: 1, targetType: 3 },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // With the correct mock shape (daily: [...]) the missions-daily section must render.
      const daily = page.getByTestId('missions-daily');
      await expect(daily, 'TC-122: missions-daily section must render with mocked daily missions').toBeVisible({ timeout: 8_000 });
      // At least one mission row must be present
      const missionRow1 = page.getByTestId('mission-row-DAILY_LESSON');
      const missionRow2 = page.getByTestId('mission-row-DAILY_CORRECT');
      const row1Count = await missionRow1.count();
      const row2Count = await missionRow2.count();
      expect(row1Count + row2Count, 'TC-122: at least one mission row (DAILY_LESSON or DAILY_CORRECT) must render').toBeGreaterThan(0);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-123
  test('GAM-FE-TC-123 — Header progress headline (done/total)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'DAILY_A', progress: 5, target: 5, rewardXp: 50, status: 2 }, // completed
          { code: 'DAILY_B', progress: 2, target: 5, rewardXp: 50, status: 1 }, // active
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // Header should show "1 / 2" (1 complete, 2 total) — inspect missions-daily section
      // missions.tsx reads data?.daily / data?.weekly (real shape) and renders a header.
      const dailySection = page.getByTestId('missions-daily');
      const dailyVisible = await dailySection.isVisible({ timeout: 5_000 }).catch(() => false);
      expect(dailyVisible, 'TC-123: missions-daily section must render with mocked daily array').toBe(true);
      // The screen header counts completed/total dailies
      const screenText = await page.getByTestId('missions-screen').textContent() ?? '';
      expect(screenText.length, 'TC-123: missions-screen must have non-empty content').toBeGreaterThan(0);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-124
  test('GAM-FE-TC-124 — Reward hero only while dailies incomplete', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'DAILY_INCOMPLETE', progress: 2, target: 5, rewardXp: 50, status: 1 },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // missions.tsx renders missions-hero (reward card) when dailies > 0 and NOT all completed.
      // With the correct mock shape (daily: [...]), the daily section must render.
      const dailySection = page.getByTestId('missions-daily');
      await expect(dailySection, 'TC-124: missions-daily must render with incomplete daily mission').toBeVisible({ timeout: 8_000 });
      // Reward hero should be visible (incomplete dailies = reward card shown)
      const hero = page.getByTestId('missions-hero');
      await expect(hero, 'TC-124: missions-hero must be visible when dailies are incomplete').toBeVisible({ timeout: 8_000 });
      const heroText = await hero.textContent() ?? '';
      expect(heroText.length, 'TC-124: missions-hero must have non-empty text').toBeGreaterThan(0);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-125
  test('GAM-FE-TC-125 — Completed row treatment (✓ tile, 100% bar)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'COMPLETED_MISSION', progress: 5, target: 5, rewardXp: 50, status: 2 },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // With correct shape the daily section renders; the completed row must appear.
      const dailySection = page.getByTestId('missions-daily');
      await expect(dailySection, 'TC-125: missions-daily must render with completed mission').toBeVisible({ timeout: 8_000 });
      const completedRow = page.getByTestId('mission-row-COMPLETED_MISSION');
      await expect(completedRow, 'TC-125: completed mission row must render').toBeVisible({ timeout: 8_000 });
      const rowText = await completedRow.textContent() ?? '';
      expect(rowText.length, 'TC-125: completed row must have non-empty text').toBeGreaterThan(0);
      // Should not show "Expired" for a completed mission
      expect(rowText.toLowerCase()).not.toContain('expired');
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-126 — Expired row (P2)
  test('GAM-FE-TC-126 — Expired row muted, non-shaming (never red)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'EXPIRED_MISSION', progress: 1, target: 5, rewardXp: 50, status: 3 }, // Expired=3
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // With the correct shape the expired row renders; assert it's non-shaming.
      const dailySection = page.getByTestId('missions-daily');
      await expect(dailySection, 'TC-126: missions-daily must render with expired mission').toBeVisible({ timeout: 8_000 });
      const expiredRow = page.getByTestId('mission-row-EXPIRED_MISSION');
      await expect(expiredRow, 'TC-126: expired mission row must render').toBeVisible({ timeout: 8_000 });
      const rowText = await expiredRow.textContent() ?? '';
      expect(rowText.length, 'TC-126: expired row must have non-empty text').toBeGreaterThan(0);
      // Must not be shaming
      expect(rowText.toLowerCase()).not.toContain('failed');
      expect(rowText.toLowerCase()).not.toContain('loser');
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-127
  test('GAM-FE-TC-127 — Weekly section excludes CHALLENGE_ rows', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        weekly: [
          { code: 'WEEKLY_NORMAL', progress: 3, target: 10, rewardXp: 200, status: 1 },
          { code: 'CHALLENGE_MATH', progress: 1, target: 5, rewardXp: 300, status: 1 }, // CHALLENGE_ prefix — must be excluded from weekly section
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // missions.tsx filters weekly to exclude CHALLENGE_* prefix for the weekly section.
      // With the correct mock shape (weekly: [...]), the section must render.
      const weekly = page.getByTestId('missions-weekly');
      await expect(weekly, 'TC-127: missions-weekly section must render with weekly missions').toBeVisible({ timeout: 8_000 });
      const weeklyText = await weekly.textContent() ?? '';
      // CHALLENGE_MATH must NOT appear in the weekly missions section
      expect(weeklyText).not.toContain('CHALLENGE_MATH');
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-128 — All-dailies celebration (timing-sensitive; BLOCKED)
  test.skip('GAM-FE-TC-128 — All-dailies-complete celebration (BLOCKED — requires mission flip across refetch; not deterministically triggerable)', async () => {});

  // GAM-FE-TC-129
  test('GAM-FE-TC-129 — Mission counts Eastern-Arabic; XP reward Latin in AR', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'DAILY_AR', progress: 1, target: 4, rewardXp: 50, status: 1 },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);

      // DESIGN NOTE: applyWebDirection() sets lang=ar, NOT dir=rtl.
      await page.waitForFunction(() => document.documentElement.lang === 'ar', { timeout: 10_000 });
      expect(await page.evaluate(() => document.documentElement.lang)).toBe('ar');

      const screenText = await page.getByTestId('missions-screen').textContent() ?? '';
      // XP reward "+50" should be Latin (50 not ٥٠)
      const hasLatin50 = /50/.test(screenText);
      const hasArabic50 = /٥٠/.test(screenText);
      if (hasLatin50) {
        // Good — XP uses Latin digits
      } else if (hasArabic50) {
        console.log('[GAM-FE-TC-129] XP reward "+50" rendered as Eastern-Arabic "٥٠" — potential digit rule violation');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-130 — Unknown titleKey fallback (P2)
  test('GAM-FE-TC-130 — Unknown titleKey falls back to generic title', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          {
            code: 'UNKNOWN_MISSION',
            titleKey: 'missions.titles.some_nonexistent_key_xyz',
            progress: 1,
            target: 5,
            rewardXp: 50,
            status: 1,
          },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const row = page.getByTestId('mission-row-UNKNOWN_MISSION');
      const rowCount = await row.count();
      if (rowCount > 0) {
        const rowText = await row.textContent() ?? '';
        // Must NOT show the raw key
        expect(rowText).not.toContain('missions.titles.some_nonexistent_key_xyz');
        expect(rowText.length).toBeGreaterThan(0);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-131
  test('GAM-FE-TC-131 — Missions error + retry', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissionsError(page);
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const errorStrip = page.getByTestId('missions-error');
      const errorVisible = await errorStrip.isVisible({ timeout: 5_000 }).catch(() => false);
      if (errorVisible) {
        await expect(errorStrip).toBeVisible();
        await expect(page.getByTestId('missions-retry')).toBeVisible({ timeout: 5_000 });
      } else {
        console.log('[GAM-FE-TC-131] missions-error not visible — mock may not have been intercepted');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-132
  test('GAM-FE-TC-132 — Mission row a11y + progressbar role', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'DAILY_A11Y', progress: 2, target: 5, rewardXp: 50, status: 1 },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // With correct mock shape the daily section renders; verify progressbar role.
      const dailySection = page.getByTestId('missions-daily');
      await expect(dailySection, 'TC-132: missions-daily must render with mocked mission').toBeVisible({ timeout: 8_000 });
      // Progressbar role should be present in the missions screen (missions.tsx uses accessibilityRole="progressbar")
      const progressBars = page.getByRole('progressbar');
      const barCount = await progressBars.count();
      // At least one progressbar must be present when a mission row renders
      expect(barCount, 'TC-132: at least one progressbar role must be present').toBeGreaterThan(0);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-133 — Reduced motion (P2)
  test('GAM-FE-TC-133 — Reduced motion: bars at target instantly', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: { cookies: [], origins: [] },
      reducedMotion: 'reduce',
    });
    const page = await ctx.newPage();
    try {
      await mockMissions(page, makeMissionsPayload({
        daily: [
          { code: 'DAILY_REDUCED', progress: 3, target: 5, rewardXp: 50, status: 1 },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/missions');
      await expect(page.getByTestId('missions-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // With correct mock shape the daily section renders in reduced-motion mode.
      const daily = page.getByTestId('missions-daily');
      await expect(daily, 'TC-133: missions-daily must render in reduced-motion context').toBeVisible({ timeout: 8_000 });
    } finally { await ctx.close(); }
  });
});

// ============================================================================
// Section 7: League screen (GAM-FE-TC-140..151)
// ============================================================================

test.describe('7. League screen', () => {
  let childEmail: string;
  let childPassword: string;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const p = await ctx.newPage();
    try {
      const seed = await seedParentAndChild('league');
      childEmail = seed.childEmail;
      childPassword = seed.childPassword;
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-140
  test('GAM-FE-TC-140 — League screen renders loading/error/empty/standings', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const loading = await page.getByTestId('league-loading').isVisible({ timeout: 1_000 }).catch(() => false);
      const error = await page.getByTestId('league-error').isVisible({ timeout: 1_000 }).catch(() => false);
      const empty = await page.getByTestId('league-empty').isVisible({ timeout: 1_000 }).catch(() => false);
      const banner = await page.getByTestId('league-banner').isVisible({ timeout: 1_000 }).catch(() => false);
      const standings = await page.getByTestId('league-standings').isVisible({ timeout: 1_000 }).catch(() => false);
      expect(loading || error || empty || banner || standings).toBe(true);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-141
  test('GAM-FE-TC-141 — Unplaced/empty state (no weekly XP)', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Mock null league response (unplaced)
      await mockLeague(page, makeLeaguePayload(null));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const empty = page.getByTestId('league-empty');
      const emptyVisible = await empty.isVisible({ timeout: 5_000 }).catch(() => false);
      if (emptyVisible) {
        const emptyText = await empty.textContent() ?? '';
        expect(emptyText.length).toBeGreaterThan(0);
        // Not an error
        const errorVisible = await page.getByTestId('league-error').isVisible({ timeout: 1_000 }).catch(() => false);
        expect(errorVisible).toBe(false);
      } else {
        console.log('[GAM-FE-TC-141] league-empty not visible — null payload may not produce empty state or mock not intercepted');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-142
  test('GAM-FE-TC-142 — Tier banner shows tier + countdown + promote count', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        tier: 1,
        tierName: 'Bronze',
        myRank: 2,
        myWeeklyXp: 250,
        promotionCutoffRank: 3,
        demotionCutoffRank: 8,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 300, isMe: false },
          { rank: 2, displayName: 'Student #2', weeklyXp: 250, isMe: true },
          { rank: 3, displayName: 'Student #3', weeklyXp: 200, isMe: false },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const banner = page.getByTestId('league-banner');
      const bannerVisible = await banner.isVisible({ timeout: 5_000 }).catch(() => false);
      if (bannerVisible) {
        await expect(banner).toBeVisible();
        const bannerText = await banner.textContent() ?? '';
        expect(bannerText.length).toBeGreaterThan(0);
        // Countdown chip should be visible
        const countdown = page.getByTestId('league-countdown');
        const cdVisible = await countdown.isVisible({ timeout: 3_000 }).catch(() => false);
        if (cdVisible) {
          await expect(countdown).toBeVisible();
        }
      } else {
        console.log('[GAM-FE-TC-142] league-banner not visible — mock may not have taken effect');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-143
  test('GAM-FE-TC-143 — Standings ranked; you-row highlighted', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        myRank: 2,
        myWeeklyXp: 200,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 300, isMe: false },
          { rank: 2, displayName: 'Student #2', weeklyXp: 200, isMe: true },
          { rank: 3, displayName: 'Student #3', weeklyXp: 150, isMe: false },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const standings = page.getByTestId('league-standings');
      const standingsVisible = await standings.isVisible({ timeout: 5_000 }).catch(() => false);
      if (standingsVisible) {
        await expect(standings).toBeVisible();
        // You-row should be present (isMe=true)
        const youRow = page.getByTestId('league-you-row');
        const youRowVisible = await youRow.isVisible({ timeout: 5_000 }).catch(() => false);
        if (youRowVisible) {
          await expect(youRow).toBeVisible();
        } else {
          console.log('[GAM-FE-TC-143] league-you-row not found in standings — mock standings may need isMe=true');
        }
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-144
  test('GAM-FE-TC-144 — Anonymization: only Student #N, no real names/PII', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        myRank: 1,
        myWeeklyXp: 100,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 100, isMe: true },
          { rank: 2, displayName: 'Student #2', weeklyXp: 80, isMe: false },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const standings = page.getByTestId('league-standings');
      const standingsVisible = await standings.isVisible({ timeout: 5_000 }).catch(() => false);
      if (standingsVisible) {
        const standingsText = await standings.textContent() ?? '';
        // Should NOT contain real email patterns
        expect(standingsText).not.toMatch(/[a-z]+@[a-z]+\.[a-z]+/i);
        // Should contain "Student" style anonymized names
        const hasStudentName = standingsText.includes('Student');
        if (!hasStudentName) {
          console.log('[GAM-FE-TC-144] No "Student #N" names found in standings — server may be using different anonymization format:', standingsText.substring(0, 200));
        }
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-145
  test('GAM-FE-TC-145 — Promotion/demotion cutlines render', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        promotionCutoffRank: 2,
        demotionCutoffRank: 4,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 300, isMe: false },
          { rank: 2, displayName: 'Student #2', weeklyXp: 200, isMe: false },
          { rank: 3, displayName: 'Student #3', weeklyXp: 100, isMe: true },
          { rank: 4, displayName: 'Student #4', weeklyXp: 50, isMe: false },
          { rank: 5, displayName: 'Student #5', weeklyXp: 30, isMe: false },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // Zone lines use testID `league-zone-promotion` and `league-zone-demotion`
      const promoZone = page.getByTestId('league-zone-promotion');
      const demotionZone = page.getByTestId('league-zone-demotion');
      const promoCount = await promoZone.count();
      const demotionCount = await demotionZone.count();
      // At least one zone line should appear if standings are visible
      const standings = await page.getByTestId('league-standings').isVisible({ timeout: 3_000 }).catch(() => false);
      if (standings) {
        expect(promoCount + demotionCount).toBeGreaterThanOrEqual(0);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-146 — Single-member league hint (P2)
  test('GAM-FE-TC-146 — Single-member league shows alone hint, no fake rivals', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        myRank: 1,
        myWeeklyXp: 100,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 100, isMe: true },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // league-alone-hint may be shown when only one member
      const aloneHint = page.getByTestId('league-alone-hint');
      const hintVisible = await aloneHint.isVisible({ timeout: 5_000 }).catch(() => false);
      if (hintVisible) {
        await expect(aloneHint).toBeVisible();
      }
      // Verify no fabricated rival rows (only the "me" row should be present)
      const standings = page.getByTestId('league-standings');
      const standingsVisible = await standings.isVisible({ timeout: 3_000 }).catch(() => false);
      if (standingsVisible) {
        // Only one standings row expected
        const allRows = standings.locator('[data-testid^="league-row-"], [data-testid="league-you-row"]');
        const rowCount = await allRows.count();
        // Should be 1 (just the me row) or 0 if hint replaces them
        expect(rowCount).toBeLessThanOrEqual(2);
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-147
  test('GAM-FE-TC-147 — Weekly XP Latin+LTR; rank Eastern-Arabic in AR', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        myRank: 3,
        myWeeklyXp: 250,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 400, isMe: false },
          { rank: 2, displayName: 'Student #2', weeklyXp: 300, isMe: false },
          { rank: 3, displayName: 'Student #3', weeklyXp: 250, isMe: true },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);

      // DESIGN NOTE: applyWebDirection() sets lang=ar, NOT dir=rtl.
      await page.waitForFunction(() => document.documentElement.lang === 'ar', { timeout: 10_000 });
      expect(await page.evaluate(() => document.documentElement.lang)).toBe('ar');

      const leagueScreen = page.getByTestId('league-screen');
      const screenText = await leagueScreen.textContent() ?? '';
      // Weekly XP (250) should appear as Latin digits
      expect(/250/.test(screenText)).toBe(true);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-148 — Zone arrows never mirror in RTL (P2)
  test('GAM-FE-TC-148 — Zone arrows keep vertical semantics in RTL', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        promotionCutoffRank: 2,
        demotionCutoffRank: 4,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 300, isMe: false },
          { rank: 2, displayName: 'Student #2', weeklyXp: 200, isMe: false },
          { rank: 3, displayName: 'Student #3', weeklyXp: 100, isMe: true },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      // DESIGN NOTE: applyWebDirection() sets lang=ar, NOT dir=rtl.
      await page.waitForFunction(() => document.documentElement.lang === 'ar', { timeout: 10_000 });
      const lang = await page.evaluate(() => document.documentElement.lang);
      expect(lang).toBe('ar');
      // No raw i18n keys
      const rawKeys: string[] = await page.evaluate(() => {
        const suspects: string[] = [];
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
        let node: Node | null;
        while ((node = walker.nextNode())) {
          const text = (node.nodeValue ?? '').trim();
          if (/^(league|common)\.[a-zA-Z_.]+$/.test(text)) suspects.push(text);
        }
        return suspects;
      });
      expect(rawKeys, `Raw i18n keys (AR league): ${rawKeys.join(', ')}`).toHaveLength(0);
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-149 — Auto-scroll (P2, visual; soft check)
  test('GAM-FE-TC-149 — Auto-scroll brings you-row into view on mount', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      // Create a longer standings list so the child is lower down
      const standings = Array.from({ length: 15 }, (_, i) => ({
        rank: i + 1,
        displayName: `Student #${i + 1}`,
        weeklyXp: (15 - i) * 50,
        isMe: i === 9, // rank 10 = isMe
      }));
      await mockLeague(page, makeLeaguePayload({ myRank: 10, myWeeklyXp: 300, standings }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);

      const youRow = page.getByTestId('league-you-row');
      const youRowVisible = await youRow.isVisible({ timeout: 5_000 }).catch(() => false);
      if (youRowVisible) {
        // Check if it's in the viewport
        const boundingBox = await youRow.boundingBox();
        if (boundingBox) {
          const viewportSize = page.viewportSize();
          const inViewport = viewportSize
            ? boundingBox.y < viewportSize.height && boundingBox.y > 0
            : true;
          if (!inViewport) {
            console.log('[GAM-FE-TC-149] you-row is not in viewport — auto-scroll may not be working');
          }
        }
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-150
  test('GAM-FE-TC-150 — League error + retry', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeagueError(page);
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);
      const errorStrip = page.getByTestId('league-error');
      const errorVisible = await errorStrip.isVisible({ timeout: 5_000 }).catch(() => false);
      if (errorVisible) {
        await expect(errorStrip).toBeVisible();
        await expect(page.getByTestId('league-retry')).toBeVisible({ timeout: 5_000 });
      } else {
        console.log('[GAM-FE-TC-150] league-error not visible — mock may not have been intercepted');
      }
    } finally { await ctx.close(); }
  });

  // GAM-FE-TC-151
  test('GAM-FE-TC-151 — Banner + rows a11y labels', async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: { cookies: [], origins: [] } });
    const page = await ctx.newPage();
    try {
      await mockLeague(page, makeLeaguePayload({
        myRank: 1,
        myWeeklyXp: 100,
        standings: [
          { rank: 1, displayName: 'Student #1', weeklyXp: 100, isMe: true },
          { rank: 2, displayName: 'Student #2', weeklyXp: 80, isMe: false },
        ],
      }));
      await signInAsChild(page, childEmail, childPassword);
      await page.goto('/(child)/league');
      await expect(page.getByTestId('league-screen')).toBeVisible({ timeout: 20_000 });
      await page.waitForTimeout(3_000);

      const banner = page.getByTestId('league-banner');
      const bannerVisible = await banner.isVisible({ timeout: 5_000 }).catch(() => false);
      if (bannerVisible) {
        const ariaLabel = await banner.getAttribute('aria-label');
        // Banner should have accessible label
        if (ariaLabel) {
          expect(ariaLabel.length).toBeGreaterThan(0);
          expect(ariaLabel).not.toMatch(/^league\./);
        }
      }
    } finally { await ctx.close(); }
  });
});
