import { expect, test } from '@playwright/test';

/** Từ khóa chắc chắn không khớp công thức nào → trạng thái rỗng ổn định, không phụ thuộc dữ liệu. */
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
    test.fixme(true, 'Chờ FR-RCP-003/005 (C) hoặc DbSeeder có công thức Published');

    await page.goto('/search?q=pho');

    const results = page.getByRole('region', { name: 'Kết quả tìm kiếm' });
    await expect(results.getByRole('link').first()).toBeVisible();
    await results.getByRole('link').first().click();
    await expect(page).toHaveURL(/\/recipes\/[^/]+$/);
  });
});
