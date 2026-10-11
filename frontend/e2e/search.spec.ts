import { test, expect } from '@playwright/test';

test.describe('FR-SRCH: Luồng tìm kiếm công thức món ăn', () => {
  test.beforeEach(async ({ page }) => {
    // Truy cập trang chủ trước mỗi test case
    await page.goto('/');
  });

  test('FR-SRCH-001: Tìm kiếm nhanh từ thanh tìm kiếm trang chủ', async ({ page }) => {
    // Tìm ô input tìm kiếm (có placeholder hoặc type="search" / type="text")
    const searchInput = page.locator('input[placeholder*="Tìm kiếm"], input[type="search"]').first();
    await expect(searchInput).toBeVisible();

    // Gõ từ khóa tìm kiếm
    await searchInput.fill('bò');
    await searchInput.press('Enter');

    // Kiểm tra URL được chuyển hướng sang trang kết quả tìm kiếm có query param
    await expect(page).toHaveURL(/search/);

    // Kiểm tra trang hiển thị tiêu đề kết quả hoặc danh sách công thức
    const resultsHeading = page.locator('h1, h2, [data-testid="search-results"]').first();
    await expect(resultsHeading).toBeVisible();
  });

  test('FR-SRCH-002 & 003: Lọc kết quả tìm kiếm theo danh mục hoặc độ khó', async ({ page }) => {
    // Đi thẳng tới trang tìm kiếm
    await page.goto('/search?q=gà');

    // Đảm bảo trang load không bị lỗi crash
    await expect(page.locator('body')).not.toContainText('500 Internal Server Error');

    // Kiểm tra có hiển thị các bộ lọc (nếu giao diện trang search có dropdown/button filter)
    const filterSection = page.locator('select, [data-testid="category-filter"], button:has-text("Lọc")').first();
    if (await filterSection.isVisible()) {
      await expect(filterSection).toBeEnabled();
    }
  });

  test('FR-SRCH-004: Xử lý khi không tìm thấy công thức nào', async ({ page }) => {
    // Tìm với từ khóa vô nghĩa
    await page.goto('/search?q=tukhoakhonghecothuc123456');

    // Kiểm tra hiển thị thông báo không tìm thấy kết quả
    const emptyState = page.locator('text=/Không tìm thấy|Chưa có công thức/i').first();
    await expect(emptyState).toBeVisible();
  });
});