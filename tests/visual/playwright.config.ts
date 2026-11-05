import { defineConfig, devices } from '@playwright/test';

const viewports = [
  { name: 'mobile-375', width: 375, height: 812 },
  { name: 'tablet-768', width: 768, height: 1024 },
  { name: 'desktop-1280', width: 1280, height: 900 },
  { name: 'wide-1440', width: 1440, height: 900 },
];

export default defineConfig({
  testDir: './specs',
  snapshotPathTemplate: '__baselines__/{projectName}/{arg}{ext}',
  fullyParallel: true,
  workers: Number(process.env.VISUAL_WORKERS ?? 4),
  retries: 1,
  timeout: 90_000,
  reporter: [['list'], ['html', { open: 'never' }]],
  expect: {
    toHaveScreenshot: { animations: 'disabled', caret: 'hide', scale: 'css', maxDiffPixels: 0 },
  },
  use: {
    baseURL: process.env.VISUAL_BASE_URL ?? 'https://localhost:44383',
    ignoreHTTPSErrors: true,
    reducedMotion: 'reduce',
    colorScheme: 'light',
    locale: 'en-GB',
    timezoneId: 'Europe/Copenhagen',
  },
  projects: viewports.map(({ name, width, height }) => ({
    name,
    use: { ...devices['Desktop Chrome'], viewport: { width, height }, deviceScaleFactor: 1 },
  })),
});
