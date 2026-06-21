import { existsSync } from 'node:fs';

import { defineConfig, devices } from '@playwright/test';

// Parent-dashboard E2E config (P5-05).
// The shared playwright.config.ts owns THREE webServers (Expo :8081, marketing :3002,
// admin :3001) and aborts the entire run when any one is unhealthy (e.g. a stale :3002
// process).  This config isolates the parent-dashboard surface: it only targets the
// Expo student-app on :8081 and never touches :3002 / :3001.
//
// Same WSL browser-deps shim + --no-sandbox as the admin config.
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
  // 24 tests × ~25 s each = ~600 s; give plenty of headroom.
  globalTimeout: 900_000,
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
      name: 'parent-chromium',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: WEB_URL,
        viewport: { width: 1280, height: 900 },
      },
      testMatch: ['**/specs/P5-05-parent-dashboard.spec.ts'],
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
