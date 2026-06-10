import { defineConfig, devices } from '@playwright/test';

/**
 * E2E config for the Learnexia student-app web PWA (Expo / React Native Web).
 *
 * Targets the web build at WEB_URL (default http://localhost:8081). The backend
 * API must be running at API_URL (default http://localhost:5080) with that origin
 * in AllowedOrigins — see ../../docs/dev/HANDOFF.md for the exact run recipe.
 *
 * Playwright owns the Expo web server below (reused if one is already running).
 * The .NET backend is a prerequisite and is NOT auto-started here (it needs the
 * Postgres stack); start it yourself per HANDOFF before running these specs.
 */
const WEB_URL = process.env.WEB_URL ?? 'http://localhost:8081';

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
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
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    // Mobile viewport — the student app is a 390/768/1024 responsive PWA.
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
  ],
  webServer: {
    command: 'EXPO_OFFLINE=1 npx expo start --port 8081',
    cwd: '../../apps/student-app',
    url: WEB_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
