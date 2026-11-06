import { expect, test, type Page } from '@playwright/test';

/**
 * E2E luồng "create recipe" + "publish" (CLAUDE.md mục 7) — FR-RCP-003 wizard, FR-RCP-004 sửa thông tin chung
 * (If-Match — D71), FR-RCP-010 quản lý bước (D6, D73), FR-RCP-009 nguyên liệu (D76), FR-RCP-005 xuất bản (D77),
 * FR-RCP-007 xóa (D78). Chạy trên backend thật: kiểm tra luôn việc
 * apiClient gắn Bearer token từ phiên Auth.js (D74) — thiếu token thì mọi lệnh ghi trả 401.
 *
 * Rate limit /auth/* (D15/D63): mỗi test đăng ký đúng một tài khoản qua UI.
 */
const PASSWORD = 'Str0ng!Pass1';
/** `next dev` biên dịch route lần đầu khá lâu — chuyển trang chờ rộng hơn 5s mặc định. */
const NAVIGATION = { timeout: 30_000 };

function uniqueEmail(): string {
  return `e2e-recipe-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
}

async function registerViaUi(page: Page) {
  await page.goto('/auth/register');
  await page.getByLabel('Tên hiển thị').fill('Bếp E2E công thức');
  await page.getByLabel('Email').fill(uniqueEmail());
  await page.getByLabel('Mật khẩu', { exact: true }).fill(PASSWORD);
  await page.getByLabel('Nhập lại mật khẩu').fill(PASSWORD);
  await page.getByRole('button', { name: 'Đăng ký' }).click();
  await expect(page).toHaveURL(/\/dashboard$/, NAVIGATION);
}

/** Tạo công thức qua wizard, trả về URL trang sửa. */
async function createViaWizard(page: Page, title: string) {
  await page.getByRole('link', { name: /Tạo công thức mới/ }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes\/new$/, NAVIGATION);

  // Bước 1 — thông tin chung.
  await page.getByLabel('Tiêu đề', { exact: true }).fill(title);
  await page.getByLabel('Mô tả ngắn').fill('Món cuốn tươi mát, dễ làm tại nhà.');
  const category = page.getByLabel('Danh mục');
  await expect(category.locator('option').nth(1)).toBeAttached();
  await category.selectOption({ index: 1 });
  await page.getByLabel('Chuẩn bị (phút)').fill('20');
  await page.getByLabel('Nấu (phút)').fill('0');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();

  // Bước 2 — nguyên liệu (một có số lượng, một "vừa đủ" — D7).
  await page.getByLabel('Tên nguyên liệu 1').fill('Tôm sú');
  await page.getByLabel('Số lượng').fill('300');
  await page.getByLabel('Đơn vị').fill('g');
  await page.getByRole('button', { name: 'Thêm nguyên liệu' }).click();
  await page.getByLabel('Tên nguyên liệu 2').fill('Muối');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();

  // Bước 3 — các bước.
  await page.getByLabel('Tiêu đề bước 1').fill('Sơ chế');
  await page.getByLabel('Mô tả', { exact: true }).first().fill('Luộc tôm, bóc vỏ.');
  await page.getByRole('button', { name: 'Thêm bước' }).click();
  await page.getByLabel('Tiêu đề bước 2').fill('Cuốn');
  await page.getByLabel('Mô tả', { exact: true }).nth(1).fill('Cuốn bánh tráng với rau.');
  await page.getByRole('button', { name: 'Tiếp tục' }).click();

  // Bước 4 — xem lại và tạo.
  await expect(page.getByRole('heading', { name: /Xem lại/ })).toBeVisible();
  await page.getByRole('button', { name: 'Tạo công thức' }).click();

  await expect(page).toHaveURL(/\/dashboard\/recipes\/[0-9a-f-]{36}\/edit$/, NAVIGATION);
  await expect(page.getByRole('heading', { level: 1, name: 'Sửa công thức' })).toBeVisible(NAVIGATION);
}

test.describe('Tạo, sửa, xuất bản, xóa công thức (FR-RCP-003/004/005/007/009/010)', () => {
  test('FR-RCP-003: wizard tạo công thức → Draft, sang trang sửa với đủ nguyên liệu và các bước', async ({ page }) => {
    await registerViaUi(page);
    const title = `Gỏi cuốn E2E ${Date.now()}`;

    await createViaWizard(page, title);

    await expect(page.getByText('Nháp', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Tiêu đề', { exact: true })).toHaveValue(title);
    await expect(page.getByRole('heading', { name: 'Nguyên liệu (2)' })).toBeVisible();
    await expect(page.getByText('vừa đủ', { exact: true })).toBeVisible();
    const steps = page.getByRole('list', { name: 'Các bước thực hiện' });
    await expect(steps.getByRole('heading', { level: 3 })).toHaveText(['Bước 1: Sơ chế', 'Bước 2: Cuốn']);
  });

  test('FR-RCP-004 + FR-RCP-010: sửa tiêu đề (If-Match), thêm / đổi chỗ / xóa bước — tải lại trang vẫn đúng', async ({
    page,
  }) => {
    await registerViaUi(page);
    await createViaWizard(page, `Bánh xèo E2E ${Date.now()}`);
    const slugText = await page.locator('header code').textContent();

    // FR-RCP-004: đổi tiêu đề, slug giữ nguyên (D13).
    await page.getByLabel('Tiêu đề', { exact: true }).fill('Bánh xèo miền Tây giòn rụm');
    await page.getByRole('button', { name: 'Lưu thông tin chung' }).click();
    await expect(page.getByText('Đã lưu thay đổi')).toBeVisible();
    // Lưu lần hai bằng rowVersion mới nhận được (D71) — không được 409.
    await page.getByLabel('Khẩu phần').fill('6');
    await page.getByRole('button', { name: 'Lưu thông tin chung' }).click();
    await expect(page.getByText('Đã lưu thay đổi').first()).toBeVisible();
    await expect(page.getByRole('alert').filter({ hasText: 'thay đổi ở nơi khác' })).toHaveCount(0);

    // FR-RCP-010: thêm bước.
    const steps = page.getByRole('list', { name: 'Các bước thực hiện' });
    await page.getByRole('button', { name: 'Thêm bước' }).click();
    await page.getByLabel('Bước mới — tiêu đề').fill('Đổ bánh');
    await page.getByLabel('Bước mới — mô tả').fill('Đổ bột vào chảo nóng.');
    await page.getByRole('button', { name: 'Thêm bước' }).click();
    await expect(steps.getByRole('heading', { level: 3 })).toHaveText(['Bước 1: Sơ chế', 'Bước 2: Cuốn', 'Bước 3: Đổ bánh']);

    // Đổi chỗ: bước 3 lên vị trí 2.
    await page.getByRole('button', { name: 'Chuyển bước 3 lên' }).click();
    await expect(steps.getByRole('heading', { level: 3 })).toHaveText(['Bước 1: Sơ chế', 'Bước 2: Đổ bánh', 'Bước 3: Cuốn']);

    // Xóa bước 1 → đánh số lại.
    await page.getByRole('button', { name: 'Xóa bước 1' }).click();
    await page.getByRole('dialog', { name: 'Xóa bước này?' }).getByRole('button', { name: 'Xóa bước' }).click();
    await expect(steps.getByRole('heading', { level: 3 })).toHaveText(['Bước 1: Đổ bánh', 'Bước 2: Cuốn']);
    await expect(page.getByText('Đã xóa bước')).toBeVisible();

    // Tải lại trang: dữ liệu lấy từ server khớp với những gì vừa làm.
    await page.reload();
    await expect(page.getByLabel('Tiêu đề', { exact: true })).toHaveValue('Bánh xèo miền Tây giòn rụm');
    await expect(page.getByLabel('Khẩu phần')).toHaveValue('6');
    await expect(page.locator('header code')).toHaveText(slugText ?? '');
    await expect(page.getByRole('list', { name: 'Các bước thực hiện' }).getByRole('heading', { level: 3 })).toHaveText([
      'Bước 1: Đổ bánh',
      'Bước 2: Cuốn',
    ]);
  });
  test('FR-RCP-009 + FR-RCP-005 + FR-RCP-007: sửa nguyên liệu, xuất bản → trang công khai xem được, rồi xóa công thức', async ({
    page,
  }) => {
    await registerViaUi(page);
    const title = `Canh chua E2E ${Date.now()}`;
    await createViaWizard(page, title);
    const editUrl = page.url();
    const slug = ((await page.locator('header code').textContent()) ?? '').replace('/recipes/', '');

    // FR-RCP-009: thêm nguyên liệu (dấu phẩy thập phân), đưa lên đầu, xóa "Muối".
    const ingredients = page.getByRole('list', { name: 'Danh sách nguyên liệu' });
    await page.getByRole('button', { name: 'Thêm nguyên liệu' }).click();
    await page.getByLabel('Nguyên liệu mới — tên').fill('Me chua');
    await page.getByLabel('Nguyên liệu mới — số lượng').fill('0,5');
    await page.getByLabel('Nguyên liệu mới — đơn vị').fill('chén');
    await page.getByRole('button', { name: 'Thêm nguyên liệu' }).click();
    await expect(page.getByText('Đã thêm nguyên liệu', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Nguyên liệu (3)' })).toBeVisible();

    await page.getByRole('button', { name: 'Chuyển Me chua lên' }).click();
    await page.getByRole('button', { name: 'Chuyển Me chua lên' }).click();
    await page.getByRole('button', { name: 'Xóa Muối' }).click();
    await page.getByRole('dialog', { name: 'Xóa nguyên liệu này?' }).getByRole('button', { name: 'Xóa nguyên liệu' }).click();
    await expect(page.getByText('Đã xóa nguyên liệu', { exact: true })).toBeVisible();

    await page.reload();
    await expect(ingredients.getByRole('listitem')).toHaveCount(2);
    await expect(ingredients.getByRole('listitem').first()).toContainText('Me chua');
    await expect(ingredients.getByRole('listitem').first()).toContainText('0,5 chén');

    // FR-RCP-005: xuất bản → badge đổi; lưu thông tin chung ngay sau đó không bị 409 (rowVersion mới — D77).
    await page.getByRole('button', { name: 'Xuất bản' }).click();
    await expect(page.getByRole('button', { name: 'Hủy xuất bản' })).toBeVisible();
    await expect(page.getByText('Đã xuất bản', { exact: true }).first()).toBeVisible();
    await page.getByLabel('Khẩu phần').fill('5');
    await page.getByRole('button', { name: 'Lưu thông tin chung' }).click();
    await expect(page.getByText('Đã lưu thay đổi')).toBeVisible();
    await expect(page.getByRole('alert').filter({ hasText: 'thay đổi ở nơi khác' })).toHaveCount(0);

    // Trang công khai (render phía server, không token — góc nhìn Guest) xem được.
    const publicPage = await page.goto(`/recipes/${slug}`, NAVIGATION);
    expect(publicPage?.status()).toBe(200);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();

    // FR-RCP-007: xóa → về /dashboard; mở lại trang sửa → không tìm thấy.
    await page.goto(editUrl, NAVIGATION);
    await page.getByRole('button', { name: 'Xóa công thức' }).click();
    await page.getByRole('dialog', { name: 'Xóa công thức này?' }).getByRole('button', { name: 'Xóa công thức' }).click();
    await expect(page).toHaveURL(/\/dashboard$/, NAVIGATION);
    await page.goto(editUrl, NAVIGATION);
    await expect(page.getByRole('heading', { name: 'Không tìm thấy công thức' })).toBeVisible(NAVIGATION);
  });
});
