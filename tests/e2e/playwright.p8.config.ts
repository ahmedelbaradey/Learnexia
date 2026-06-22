/**
 * P8 Learning-Language E2E config.
 *
 * Same isolation strategy as playwright.parent.config.ts:
 * - Only targets student-app at :8081 (no :3002/:3001).
 * - reuseExistingServer=true → stack must already be running.
 * - workers:1 + fullyParallel:false to prevent Metro bundler degradation.
 * - All tests tagged P8-01-FE, P8-04-FE, P8-SHELL, P8-AXIS.
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
  testMatch: '**/P8-learning-language.spec.ts',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['html', { open: 'never' }], ['list']],
  globalTimeout: 1_200_000,
  timeout: 180_000,
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
      name: 'p8-chromium',
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
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
