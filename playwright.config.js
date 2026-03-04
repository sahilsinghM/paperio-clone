import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30000,
  webServer: {
    command: 'python3 -m http.server 8767',
    port: 8767,
    reuseExistingServer: true,
  },
  use: {
    baseURL: 'http://localhost:8767',
  },
});
