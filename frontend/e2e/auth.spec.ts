import { expect, test } from '@playwright/test';

/**
 * E2E luồng xác thực — CLAUDE.md mục 7 (5 luồng: register, login, …), FR-AUTH-001/002, D61.
 *
 * Rate limit /auth/* (NFR-SEC-003, D15) đếm theo IP, mà khi chạy local mọi request đều từ
 * 127.0.0.1 (Development nâng ngưỡng lên 100/phút — D62). File này vẫn giữ ít request auth: chạy
 * tuần tự, mỗi test đúng một lần đăng ký/đăng nhập, để chạy được cả với ngưỡng thật 10/phút.
 */
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';
const PASSWORD = 'Str0ng!Pass1';

function uniqueEmail(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
}

test.describe.configure({ mode: 'serial' });

test.describe('Xác thực (FR-AUTH-001/002, D61)', () => {
  const login = { email: uniqueEmail('e2e-login'), displayName: 'Người dùng E2E' };

  test.beforeAll(async () => {
    // Tài khoản có sẵn cho các test đăng nhập — tạo bằng API để không phụ thuộc UI đăng ký.
    const res = await fetch(`${API_BASE_URL}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: login.email, password: PASSWORD, displayName: login.displayName }),
    });
    expect(res.status, 'đăng ký tài khoản E2E qua API').toBe(201);
  });

  test('FR-AUTH-001: đăng ký tài khoản mới → tự đăng nhập và vào /dashboard', async ({ page }) => {
    const displayName = 'Bếp E2E';

    await page.goto('/auth/register');
    await page.getByLabel('Tên hiển thị').fill(displayName);
    await page.getByLabel('Email').fill(uniqueEmail('e2e-register'));
    await page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD);
    await page.getByLabel('Nhập lại mật khẩu').fill(PASSWORD);
    await page.getByRole('button', { name: 'Đăng ký' }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByRole('heading', { level: 1, name: `Chào mừng, ${displayName}!` })).toBeVisible();
  });

  test('FR-AUTH-002: đăng nhập đúng → vào /dashboard', async ({ page }) => {
    await page.goto('/auth/login');
    await page.getByLabel('Email').fill(login.email);
    await page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByRole('heading', { level: 1, name: `Chào mừng, ${login.displayName}!` })).toBeVisible();
  });

  test('FR-AUTH-002: sai mật khẩu → báo lỗi chung, không lộ email có tồn tại hay không', async ({ page }) => {
    await page.goto('/auth/login');
    await page.getByLabel('Email').fill(login.email);
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Sai!Mat1khau');
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();

    const alert = page.getByRole('alert').filter({ hasText: 'Email hoặc mật khẩu không đúng.' });
    await expect(alert).toBeVisible();
    await expect(alert).not.toContainText(/chưa đăng ký|không tồn tại/i);
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('D61: chưa đăng nhập vào /dashboard/profile → chuyển về đăng nhập, đăng nhập xong quay lại đúng trang', async ({
    page,
  }) => {
    await page.goto('/dashboard/profile');

    await expect(page).toHaveURL(/\/auth\/login\?callbackUrl=%2Fdashboard%2Fprofile/);

    await page.getByLabel('Email').fill(login.email);
    await page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();

    await expect(page).toHaveURL(/\/dashboard\/profile$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Hồ sơ của tôi' })).toBeVisible();
  });

  test('D61: chưa đăng nhập vào /dashboard → chuyển về /auth/login kèm callbackUrl', async ({ page }) => {
    await page.goto('/dashboard');

    await expect(page).toHaveURL(/\/auth\/login\?callbackUrl=%2Fdashboard$/);
    await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  });
});
