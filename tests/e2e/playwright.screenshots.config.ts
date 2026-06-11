/**
 * Playwright config specifically for the p12-screenshots spec.
 * Only registers the student-app web server (no marketing site dependency).
 * Use: npx playwright test --config=playwright.screenshots.config.ts
 */
import { defineConfig, devices } from '@playwright/test';

const WEB_URL = process.env.WEB_URL ?? 'http://localhost:8081';

export default defineConfig({
  testDir: './specs',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 300_000,
  use: {
    baseURL: WEB_URL,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
    navigationTimeout: 60_000,
    actionTimeout: 30_000,
  },
  projects: [
    {
      name: 'mobile',
      use: {
        ...devices['Pixel 7'],
        viewport: { width: 390, height: 844 },
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
