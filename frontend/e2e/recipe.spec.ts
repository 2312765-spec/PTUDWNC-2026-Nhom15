import { test, expect } from '@playwright/test';

test.describe('FR-RCP: Luồng xem danh sách và chi tiết công thức', () => {
  test('FR-RCP-001: Hiển thị danh mục và công thức mới nhất trên trang chủ', async ({ page }) => {
    await page.goto('/');

    // Kiểm tra hiển thị tiêu đề trang chủ
    await expect(page.locator('text=Khám Phá Hương Vị Mới')).toBeVisible();

    // Kiểm tra hiển thị mục Danh Mục Nổi Bật
    await expect(page.locator('text=Danh Mục Nổi Bật')).toBeVisible();

    // Kiểm tra hiển thị mục Công Thức Mới Nhất
    await expect(page.locator('text=Công Thức Mới Nhất')).toBeVisible();

    // Kiểm tra có ít nhất một thẻ công thức món ăn (link dẫn tới /recipes/...)
    const recipeCards = page.locator('a[href^="/recipes/"]');
    await expect(recipeCards.first()).toBeVisible();
  });

  test('FR-RCP-002: Bấm vào thẻ công thức để xem chi tiết món ăn', async ({ page }) => {
    await page.goto('/');

    // Lấy thẻ công thức đầu tiên trên trang chủ và click vào
    const firstRecipe = page.locator('a[href^="/recipes/"]').first();
    await expect(firstRecipe).toBeVisible();
    await firstRecipe.click();

    // Đảm bảo URL đã chuyển sang trang chi tiết công thức
    await expect(page).toHaveURL(/\/recipes\/.+/);

    // Kiểm tra các phần quan trọng trên trang chi tiết công thức
    // 1. Tiêu đề món ăn
    const recipeTitle = page.locator('h1').first();
    await expect(recipeTitle).toBeVisible();

    // 2. Phần Nguyên liệu (Ingredients)
    const ingredientsSection = page.locator('text=/Nguyên liệu|Ingredients/i').first();
    await expect(ingredientsSection).toBeVisible();

    // 3. Phần Các bước thực hiện (Instructions / Steps)
    const instructionsSection = page.locator('text=/Cách làm|Các bước|Hướng dẫn/i').first();
    await expect(instructionsSection).toBeVisible();
  });
});