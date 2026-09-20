import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end configuration.
 *
 * These tests drive the built application against a **running CapFrameX service**, not a mock: the
 * chart gate is a statement about real captures, and a fixture would measure the fixture. The dev
 * host is started here because it is cheap and stateless; the service is not, because it needs
 * administrator rights and owns the database.
 *
 * Port 4200 is not a preference: it is the origin the service's guard accepts.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 900 },
      },
    },
  ],
  webServer: {
    command: 'node tools/dev-host.mjs',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    timeout: 30_000,
  },
});
