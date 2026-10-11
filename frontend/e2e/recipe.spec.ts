import { expect, test } from '@playwright/test';

/**
 * Chờ dữ liệu: chưa có cách tạo công thức Published cho E2E — API tạo/publish (FR-RCP-003/005/
 * 009/010, C) còn Pending và DbSeeder đang trống. Bỏ `fixme` khi một trong hai sẵn sàng.
 */
const NEEDS_PUBLISHED_RECIPE = 'Chờ FR-RCP-003/005 (C) hoặc DbSeeder có công thức Published';

test.describe('Trang chủ (FR-RCP-001)', () => {
  test('FR-RCP-001: hiện hero, ô tìm kiếm, danh mục và công thức mới nhất — không có lỗi tải', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { level: 1, name: /Nấu ngon mỗi ngày cùng Culinary Blog/ })).toBeVisible();
    await expect(page.getByRole('search').getByRole('searchbox', { name: 'Từ khóa tìm kiếm' })).toBeVisible();

    const categories = page.getByRole('region', { name: 'Danh mục món ăn' });
    const latest = page.getByRole('region', { name: 'Công thức mới nhất' });
    await expect(categories).toBeVisible();
    await expect(latest).toBeVisible();
    // Backend sống (global-setup) → không phần nào được rơi vào trạng thái lỗi.
    await expect(categories.getByRole('alert')).toHaveCount(0);
    await expect(latest.getByRole('alert')).toHaveCount(0);
  });

  test('FR-CAT-001: "Xem tất cả" ở trang chủ dẫn tới /categories', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('region', { name: 'Danh mục món ăn' }).getByRole('link', { name: /Xem tất cả/ }).click();

    await expect(page).toHaveURL(/\/categories$/);
  });

  test('FR-RCP-001/002: bấm card công thức ở trang chủ → trang chi tiết có Nguyên liệu và Các bước thực hiện', async ({
    page,
  }) => {
    test.fixme(true, NEEDS_PUBLISHED_RECIPE);

    await page.goto('/');
    const card = page.getByRole('region', { name: 'Công thức mới nhất' }).getByRole('link').first();
    const title = (await card.getByRole('heading').textContent())?.trim() ?? '';
    await card.click();

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
  });
});
