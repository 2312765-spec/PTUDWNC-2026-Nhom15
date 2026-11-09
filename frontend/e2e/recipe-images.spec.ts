import { expect, test, type Page } from '@playwright/test';

/**
 * E2E NFR-USE-004 cho ảnh công thức (FR-RCP-008, D22/D27): đặt ảnh chính và xoá ảnh là optimistic —
 * giao diện đổi NGAY khi bấm, API lỗi thì hoàn tác + toast lỗi. Chạy trên backend thật (upload thật),
 * chỉ chặn PATCH/DELETE ảnh bằng `page.route`.
 *
 * Rate limit /auth/* (D15/D63): test đăng ký đúng một tài khoản qua UI.
 */
const PASSWORD = 'Str0ng!Pass1';
const NAVIGATION = { timeout: 30_000 };

/** PNG 1×1 hợp lệ — qua được kiểm tra magic bytes của server (D28). */
const PNG_1X1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

function uniqueEmail(): string {
  return `e2e-images-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
}

async function registerViaUi(page: Page) {
  await page.goto('/auth/register');
  await page.getByLabel('Tên hiển thị').fill('Bếp E2E ảnh');
  await page.getByLabel('Email').fill(uniqueEmail());
  await page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD);
  await page.getByLabel('Nhập lại mật khẩu').fill(PASSWORD);
  await page.getByRole('button', { name: 'Đăng ký' }).click();
  await expect(page).toHaveURL(/\/dashboard$/, NAVIGATION);
}

/** Tạo nhanh một công thức nháp qua wizard (đủ trường bắt buộc), dừng ở trang sửa. */
async function createDraftRecipe(page: Page) {
  await page.getByRole('link', { name: /Tạo công thức mới/ }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes\/new$/, NAVIGATION);

  await page.getByLabel('Tiêu đề', { exact: true }).fill(`Bánh xèo ảnh E2E ${Date.now()}`);
  await page.getByLabel('Mô tả ngắn').fill('Bánh xèo giòn rụm, dùng để thử quản lý ảnh.');
  const category = page.getByLabel('Danh mục');
  await expect(category.locator('option').nth(1)).toBeAttached();
  await category.selectOption({ index: 1 });
  await page.getByLabel('Chuẩn bị (phút)').fill('15');
  await page.getByLabel('Nấu (phút)').fill('20');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();

  await page.getByLabel('Tên nguyên liệu 1').fill('Bột gạo');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();

  await page.getByLabel('Tiêu đề bước 1').fill('Đổ bánh');
  await page.getByLabel('Mô tả', { exact: true }).first().fill('Đổ bột vào chảo nóng.');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();

  await page.getByRole('button', { name: 'Tạo công thức' }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes\/[0-9a-f-]{36}\/edit$/, NAVIGATION);
}

/** Cổng chặn: request bị giữ lại tới khi gọi `release()` — để kiểm giao diện đã đổi trước khi server trả lời. */
function createGate() {
  let release!: () => void;
  const opened = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { opened, release };
}

test('NFR-USE-004: đặt ảnh chính / xoá ảnh đổi giao diện ngay; API lỗi → hoàn tác, toast lỗi; server không đổi', async ({
  page,
}) => {
  test.slow();
  await registerViaUi(page);
  await createDraftRecipe(page);

  const section = page.locator('section', { has: page.getByRole('heading', { name: 'Ảnh minh họa' }) });
  const cards = section.locator('div.grid > div');
  const count = section.getByText(/^Ảnh của bạn \(\d+\)$/);

  // Upload thật 2 ảnh — ảnh đầu tự là primary (D22).
  await section.getByLabel('Chọn ảnh để tải lên').setInputFiles([
    { name: 'mot.png', mimeType: 'image/png', buffer: PNG_1X1 },
    { name: 'hai.png', mimeType: 'image/png', buffer: PNG_1X1 },
  ]);
  await expect(count).toHaveText('Ảnh của bạn (2)', NAVIGATION);
  await expect(section.getByText(/Đang tải/)).toHaveCount(0);
  await expect(cards.nth(0).getByText('Ảnh chính')).toBeVisible();

  // Mọi PATCH/DELETE lên một ảnh bị giữ lại tới khi mở cổng, rồi trả 500.
  let gate = createGate();
  await page.route('**/api/v1/recipes/*/images/*', async (route) => {
    const method = route.request().method();
    if (method !== 'PATCH' && method !== 'DELETE') return route.continue();
    await gate.opened;
    return route.fulfill({
      status: 500,
      contentType: 'application/problem+json',
      body: JSON.stringify({ type: 'about:blank', title: 'Internal Server Error', status: 500 }),
    });
  });

  // Đặt ảnh chính: badge chuyển sang ảnh 2 NGAY (server còn chưa trả lời) → lỗi → về ảnh 1.
  await cards.nth(1).getByRole('button', { name: 'Đặt làm ảnh chính' }).click();
  await expect(cards.nth(1).getByText('Ảnh chính')).toBeVisible();
  await expect(cards.nth(0).getByText('Ảnh chính')).toHaveCount(0);
  gate.release();
  const updateError = page.getByRole('alert').filter({ hasText: 'Cập nhật ảnh thất bại' });
  await expect(updateError).toBeVisible();
  await expect(cards.nth(0).getByText('Ảnh chính')).toBeVisible();
  await expect(cards.nth(1).getByText('Ảnh chính')).toHaveCount(0);

  // Xoá ảnh 2: dialog đóng + ảnh biến mất NGAY → lỗi → ảnh quay lại.
  gate = createGate();
  await cards.nth(1).getByRole('button', { name: 'Xoá ảnh' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xoá ảnh' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(count).toHaveText('Ảnh của bạn (1)');
  gate.release();
  await expect(page.getByRole('alert').filter({ hasText: 'Xoá ảnh thất bại' })).toBeVisible();
  await expect(count).toHaveText('Ảnh của bạn (2)');
  await expect(cards.nth(0).getByText('Ảnh chính')).toBeVisible();

  // Server không bị đổi gì.
  await page.unroute('**/api/v1/recipes/*/images/*');
  await page.reload();
  await expect(count).toHaveText('Ảnh của bạn (2)', NAVIGATION);
  await expect(cards.nth(0).getByText('Ảnh chính')).toBeVisible();
  await expect(cards.nth(1).getByText('Ảnh chính')).toHaveCount(0);
});
