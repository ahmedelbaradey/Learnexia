import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs',
  testMatch: '**/parent-final-capture.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-final' }]],
  timeout: 600_000,
  use: {
    baseURL: 'http://localhost:8081',
    trace: 'on',
    screenshot: 'on',
    video: 'on',
    navigationTimeout: 180_000,
    actionTimeout: 120_000,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 900 },
      },
    },
  ],
});
