import { expect, test } from '@playwright/test';

/**
 * @file E2E tests cho luồng Tìm kiếm công thức (FR-SRCH-001).
 * ĐIỀU KIỆN TIÊN QUYẾT: DB đã có dữ liệu mẫu của DbSeeder (API chạy ở Development tự seed).
 */

/** Từ khóa chắc chắn không khớp công thức nào → trạng thái rỗng ổn định. */
const NO_MATCH = 'zzkhongtontaie2e';

/**
 * Công thức Published có sẵn trong SeedRecipeCatalog ("Khoai lang lắc phô mai"; DbSeeder gán
 * trạng thái theo i % 3 nên công thức này là Published). Phải gõ "phô mai" hai chữ: search vector
 * tách thành 2 token 'pho', 'mai' — "phomai" viết liền không khớp gì (D49).
 */
const SEED_MATCH = { q: 'phô mai', title: 'Khoai lang lắc phô mai', slug: 'khoai-lang-lac-pho-mai' };

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
    await page.goto(`/search?q=${encodeURIComponent(SEED_MATCH.q)}`);

    const results = page.getByRole('region', { name: 'Kết quả tìm kiếm' });
    await expect(results).toBeVisible();

    const card = results.getByRole('link', { name: new RegExp(SEED_MATCH.title) });
    await expect(card).toBeVisible();
    await card.click();

    // Lần đầu dev server còn phải biên dịch trang chi tiết → nới timeout so với mặc định 5s.
    await expect(page).toHaveURL(new RegExp(`/recipes/${SEED_MATCH.slug}$`), { timeout: 15_000 });
  });
});
