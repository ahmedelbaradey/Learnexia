import { existsSync } from 'node:fs';

import { defineConfig, devices } from '@playwright/test';

// Admin-only E2E config. The shared playwright.config.ts owns three webServers
// (Expo :8081, marketing :3002, admin :3001); when any of those is unhealthy
// Playwright aborts the whole run with EADDRINUSE before a single admin test
// executes. This config isolates the admin-dashboard surface: it only starts /
// reuses the admin Next.js server on :3001 and never touches :8081 / :3002.
//
// Same WSL browser-deps shim as the shared config.
const PW_DEPS = `${process.env.HOME}/.pw-deps/extracted/usr/lib/x86_64-linux-gnu`;
if (existsSync(PW_DEPS)) {
  process.env.LD_LIBRARY_PATH = [PW_DEPS, process.env.LD_LIBRARY_PATH].filter(Boolean).join(':');
}

const ADMIN_URL = process.env.ADMIN_URL ?? 'http://localhost:3001';

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 2,
  reporter: [['html', { open: 'never' }], ['list']],
  timeout: 180_000,
  use: {
    baseURL: ADMIN_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    navigationTimeout: 60_000,
    actionTimeout: 30_000,
    launchOptions: { args: ['--no-sandbox'] },
  },
  projects: [
    {
      name: 'admin',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: ADMIN_URL,
      },
      testMatch: '**/specs/P7-admin-*.spec.ts',
    },
  ],
  webServer: [
    {
      command: 'NEXT_PUBLIC_API_URL=http://localhost:5080 pnpm --filter @learnexia/admin-dashboard dev',
      cwd: '../../',
      url: ADMIN_URL,
      reuseExistingServer: true,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
      env: {
        NEXT_PUBLIC_API_URL: 'http://localhost:5080',
      },
    },
  ],
});
