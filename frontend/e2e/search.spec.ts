import { expect, test } from '@playwright/test';

/**
 * @file E2E tests cho luồng Tìm kiếm công thức (FR-SRCH-001).
 * ĐIỀU KIỆN TIÊN QUYẾT: Cơ sở dữ liệu đã được nạp dữ liệu mẫu bởi DbSeeder (Seeding/DbSeeder.cs).
 * DbSeeder gán trạng thái RecipeStatus theo công thức (i % 3):
 * 0 = Draft, 1 = Published, 2 = Archived.
 */

/** Từ khóa chắc chắn không khớp công thức nào → trạng thái rỗng ổn định. */
const NO_MATCH = 'zzkhongtontaie2e';

test.describe('Luồng tìm kiếm (FR-SRCH-001)', () => {
  test('FR-SRCH-001: gõ từ khóa ở trang chủ → sang /search?q= và hiện kết quả cho đúng từ khóa', async ({ page }) => {
    await page.goto('/');

    const search = page.getByRole('search');
    await search.getByRole('searchbox', { name: 'Từ khóa tìm kiếm' }).fill(`  ${NO_MATCH} `);
    await search.getByRole('button', { name: 'Tìm kiếm' }).click();

    await expect(page).toHaveURL(new RegExp(`/search\\?q=${NO_MATCH}$`));
    await expect(page.getByRole('heading', { level: 1, name: 'Tìm kiếm công thức' })).toBeVisible();
    // Ô tìm kiếm ở /search giữ lại từ khóa (đã trim).
    await expect(page.getByRole('searchbox', { name: 'Từ khóa tìm kiếm' })).toHaveValue(NO_MATCH);
    await expect(page.getByRole('status')).toContainText(`Không tìm thấy công thức nào cho "${NO_MATCH}"`);
  });

  test('FR-SRCH-001: từ khóa < 2 ký tự → báo lỗi tiếng Việt, ở lại trang chủ', async ({ page }) => {
    await page.goto('/');

    const search = page.getByRole('search');
    await search.getByRole('searchbox', { name: 'Từ khóa tìm kiếm' }).fill('a');
    await search.getByRole('button', { name: 'Tìm kiếm' }).click();

    await expect(search.getByRole('alert')).toContainText('ít nhất 2 ký tự');
    await expect(page).toHaveURL(/\/$/);
  });

  test('FR-SRCH-001: mở thẳng /search?q= dưới 2 ký tự → không tìm, chỉ hiện form', async ({ page }) => {
    await page.goto('/search?q=a');

    await expect(page.getByRole('heading', { level: 1, name: 'Tìm kiếm công thức' })).toBeVisible();
    await expect(page.getByRole('status')).toHaveCount(0);
    await expect(page.getByRole('region', { name: 'Kết quả tìm kiếm' })).toHaveCount(0);
  });

  test('FR-SRCH-001: có công thức khớp → hiện danh sách kết quả, bấm vào sang trang chi tiết', async ({ page }) => {
    // Tìm kiếm với từ khóa "phomai", nhắm vào công thức có (index % 3 == 1) chắc chắn Published từ DbSeeder
    await page.goto('/search?q=phomai');

    const results = page.getByRole('region', { name: 'Kết quả tìm kiếm' });
    await expect(results).toBeVisible();

    const firstCard = results.getByRole('link').first();
    await expect(firstCard).toBeVisible();

    // Kiểm tra tiêu đề của kết quả đầu tiên xuất hiện
    await expect(firstCard.getByRole('heading')).toBeVisible();

    // Bấm vào kết quả đầu tiên để chuyển sang trang chi tiết công thức
    await firstCard.click();
    await expect(page).toHaveURL(/\/recipes\/[^/]+$/);
  });
});