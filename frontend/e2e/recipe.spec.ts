import { expect, test } from '@playwright/test';

test.describe('Trang chủ (FR-RCP-001, D97)', () => {
  test('FR-RCP-001: hiện tiêu đề, ô tìm kiếm, Mục lục danh mục, Món đáng thử và Thực đơn mới — không có lỗi tải', async ({
    page,
  }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { level: 1, name: 'Hôm nay nấu gì đây?' })).toBeVisible();
    await expect(page.getByRole('search').getByRole('searchbox', { name: 'Từ khóa tìm kiếm' })).toBeVisible();

    // D97: danh mục nằm ở Mục lục của thanh bên (viewport desktop ≥ 1200px → bản cột trái hiện).
    const toc = page.getByRole('navigation', { name: 'Mục lục' });
    await expect(toc).toBeVisible();
    await expect(toc.getByRole('link').first()).toBeVisible();

    await expect(page.getByRole('region', { name: 'Món đáng thử' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Thực đơn mới' })).toBeVisible();
    // Backend sống (global-setup) → không phần nào rơi vào trạng thái lỗi. Chỉ xét <main>: bộ thông báo chuyển
    // trang của Next (next-route-announcer) cũng có role="alert".
    await expect(page.getByRole('main').getByRole('alert')).toHaveCount(0);
  });

  test('FR-CAT-001: link "Danh mục" trên thanh bên dẫn tới /categories', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Danh mục' }).click();

    await expect(page).toHaveURL(/\/categories$/);
  });

  test('FR-RCP-001/002: bấm món trong Thực đơn mới → trang chi tiết có Nguyên liệu và Các bước thực hiện', async ({
    page,
  }) => {
    // Dữ liệu: DbSeeder (Development) có sẵn công thức Published.
    await page.goto('/');
    const link = page.getByRole('region', { name: 'Thực đơn mới' }).getByRole('listitem').first().getByRole('link');
    const title = (await link.textContent())?.trim() ?? '';
    await link.click();

    await expect(page).toHaveURL(/\/recipes\/[^/]+$/);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: 'Nguyên liệu' })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: 'Các bước thực hiện' })).toBeVisible();
  });
});

test.describe('Chi tiết công thức (FR-RCP-002)', () => {
  test('FR-RCP-002: slug không tồn tại → 404', async ({ page }) => {
    const response = await page.goto('/recipes/khong-ton-tai-e2e');

    expect(response?.status()).toBe(404);
    await expect(page.getByRole('heading', { level: 1, name: 'Không tìm thấy công thức' })).toBeVisible();
  });
});

test.describe('Chi tiết danh mục (FR-CAT-002)', () => {
  test('FR-CAT-002: slug không tồn tại → 404', async ({ page }) => {
    const response = await page.goto('/categories/khong-ton-tai-e2e');

    expect(response?.status()).toBe(404);
    await expect(page.getByRole('heading', { level: 1, name: 'Không tìm thấy danh mục' })).toBeVisible();
  });
});
