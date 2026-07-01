/**
 * Focused config for quiz E2E re-run (P2-05, P2-06, P2-07, carryover-d1, P1-forgot-reset).
 * All servers are expected to be already running (reuseExistingServer: true for all).
 * Does NOT try to start admin/marketing — assumes they're up or irrelevant.
 */
import { existsSync } from 'node:fs';

import { defineConfig, devices } from '@playwright/test';

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
  forbidOnly: false,
  retries: 0,
  workers: 1,
  reporter: [['html', { open: 'never' }], ['list']],
  globalTimeout: 7_200_000, // 2 hours
  timeout: 240_000,
  use: {
    baseURL: WEB_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    navigationTimeout: 60_000,
    actionTimeout: 30_000,
    launchOptions: { args: ['--no-sandbox'] },
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: WEB_URL,
        viewport: { width: 1280, height: 900 },
      },
    },
  ],
  webServer: [
    {
      command: 'EXPO_OFFLINE=1 npx expo start --port 8081',
      cwd: '../../apps/student-app',
      url: WEB_URL,
      reuseExistingServer: true,
      timeout: 60_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
