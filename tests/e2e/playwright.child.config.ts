/**
 * Child (student-app) E2E config.
 *
 * Mirrors playwright.parent.config.ts but targets child-surface specs instead of
 * the parent dashboard. Only targets the Expo student-app on :8081; never touches
 * the admin (:3001) or marketing (:3002) web servers, avoiding the three-webServer
 * abort that the default playwright.config.ts can trigger when those are not running.
 *
 * Usage:
 *   cd tests/e2e
 *   npx playwright test --config=playwright.child.config.ts --workers=1 P4-12
 */
import { existsSync } from 'node:fs';

import { defineConfig, devices } from '@playwright/test';

// WSL2 browser-deps shim (same as sibling configs).
const PW_DEPS = `${process.env.HOME}/.pw-deps/extracted/usr/lib/x86_64-linux-gnu`;
if (existsSync(PW_DEPS)) {
  process.env.LD_LIBRARY_PATH = [PW_DEPS, process.env.LD_LIBRARY_PATH]
    .filter(Boolean)
    .join(':');
}

const WEB_URL = process.env.WEB_URL ?? 'http://localhost:8081';

export default defineConfig({
  testDir: './specs',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['html', { open: 'never' }], ['list']],
  // Budget: up to 11 tests × ~80 s each (API seed + page load + assertions).
  globalTimeout: 3_600_000,
  timeout: 120_000,
  use: {
    baseURL: WEB_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    navigationTimeout: 60_000,
    actionTimeout: 30_000,
    launchOptions: { args: ['--no-sandbox'] },
  },
  projects: [
    {
      name: 'child-chromium',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: WEB_URL,
        // Use desktop viewport — matches sibling configs and avoids mobile layout quirks.
        viewport: { width: 1280, height: 900 },
      },
      // Match all child-surface specs.  Add new stories here as they land.
      testMatch: [
        '**/specs/P4-12-timed-event-participation.spec.ts',
        '**/specs/P4-gamification-xp-streak-hearts.spec.ts',
        '**/specs/P4-gamification-badges-missions-league.spec.ts',
        '**/specs/P4-gamification-events-shell.spec.ts',
      ],
    },
  ],
  webServer: [
    {
      // Reuse the already-running Expo dev server; never start a second instance.
      command: 'EXPO_OFFLINE=1 npx expo start --port 8081',
      cwd: '../../apps/student-app',
      url: WEB_URL,
      reuseExistingServer: true,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
