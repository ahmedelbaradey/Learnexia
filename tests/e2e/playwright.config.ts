import { existsSync } from 'node:fs';

import { defineConfig, devices } from '@playwright/test';

// WSL / no-root browser deps: this Ubuntu 24.04 box has no sudo, so chromium's
// system libs (libnspr4 / libnss3 / libasound2t64) were fetched via `apt-get
// download` and extracted under ~/.pw-deps (see docs/dev/HANDOFF.md). Point the
// browser subprocess at them so Playwright can launch headless without root.
const PW_DEPS = `${process.env.HOME}/.pw-deps/extracted/usr/lib/x86_64-linux-gnu`;
if (existsSync(PW_DEPS)) {
  process.env.LD_LIBRARY_PATH = [PW_DEPS, process.env.LD_LIBRARY_PATH].filter(Boolean).join(':');
}

/**
 * E2E config for the Learnexia student-app web PWA (Expo / React Native Web)
 * AND the marketing site (Next.js 15 on :3002).
 *
 * Student-app targets WEB_URL (default http://localhost:8081). The backend
 * API must be running at API_URL (default http://localhost:5080) with that origin
 * in AllowedOrigins — see ../../docs/dev/HANDOFF.md for the exact run recipe.
 *
 * Marketing site targets MARKETING_URL (default http://localhost:3002). The
 * Next.js dev server is auto-started via the second webServer entry below.
 *
 * Playwright owns both web servers below (reused if already running).
 * The .NET backend is a prerequisite for student-app specs and is NOT
 * auto-started here; start it yourself per HANDOFF before running those specs.
 */
const WEB_URL = process.env.WEB_URL ?? 'http://localhost:8081';
const MARKETING_URL = process.env.MARKETING_URL ?? 'http://localhost:3002';

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  // Limit to 2 workers in local dev to prevent Metro bundler degradation under
  // heavy parallel load (Expo Metro gets unstable when many tests run simultaneously).
  // CI uses 1 worker for full determinism.
  workers: process.env.CI ? 1 : 2,
  reporter: [['html', { open: 'never' }], ['list']],
  // Global test timeout — applies to tests AND beforeAll/beforeEach hooks.
  // P1-11-FE setup flows (register + add-child) can take up to 2 minutes per group.
  timeout: 180_000,
  use: {
    baseURL: WEB_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    // Cap individual navigation + action timeouts so a slow dev server can't
    // consume the entire test-level budget (test.setTimeout may raise test timeout
    // to 480 s but page.goto should never wait that long).
    navigationTimeout: 60_000,
    actionTimeout: 30_000,
    // WSL needs the chromium sandbox disabled (no user namespaces under WSL2).
    launchOptions: { args: ['--no-sandbox'] },
  },
  projects: [
    // ── Student-app projects (Expo / React Native Web at :8081) ──────────────
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    // Mobile viewport — the student app is a 390/768/1024 responsive PWA.
    { name: 'mobile', use: { ...devices['Pixel 7'] } },

    // ── Marketing-site projects (Next.js 15 at :3002) ─────────────────────────
    // Specs under specs/ that use the marketing project set baseURL via project
    // use override. Each marketing spec imports { test, expect } from the
    // @playwright/test standard import; project selection is via --project flag
    // or grep. Marketing specs must be named *.spec.ts and should declare
    // `test.use({ baseURL: MARKETING_URL })` at the top of the file OR rely on
    // the project-level baseURL override below.
    {
      name: 'marketing',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: MARKETING_URL,
      },
      // Only run specs that opt into the marketing project (testMatch below).
      testMatch: '**/specs/marketing-*.spec.ts',
    },
    {
      name: 'marketing-mobile',
      use: {
        ...devices['Pixel 7'],
        baseURL: MARKETING_URL,
        viewport: { width: 390, height: 844 },
      },
      testMatch: '**/specs/marketing-*.spec.ts',
    },
  ],
  webServer: [
    // Student-app (Expo web on :8081)
    {
      command: 'EXPO_OFFLINE=1 npx expo start --port 8081',
      cwd: '../../apps/student-app',
      url: WEB_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    // Marketing site (Next.js 15 on :3002)
    // Uses pnpm --filter so the workspace turbo cache is respected.
    // reuseExistingServer: true locally so `pnpm dev` started manually is reused.
    {
      command: 'pnpm --filter @learnexia/marketing-site dev',
      cwd: '../../',
      url: MARKETING_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
