import { defineConfig, devices } from '@playwright/test';

/**
 * E2E — CLAUDE.md mục 7 (NFR-MAINT-002). Chạy: `npm run test:e2e`.
 *
 * Cần backend + hạ tầng chạy sẵn (`docker compose up -d` và
 * `dotnet run --project backend/CulinaryBlog.API`) — e2e/global-setup.ts kiểm tra trước và báo
 * lỗi rõ nếu chưa bật. Frontend do `webServer` tự bật (hoặc dùng lại server :3000 đang chạy).
 */
const BASE_URL = process.env.E2E_BASE_URL ?? 'http://localhost:3000';

export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: BASE_URL,
    locale: 'vi-VN',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev',
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
