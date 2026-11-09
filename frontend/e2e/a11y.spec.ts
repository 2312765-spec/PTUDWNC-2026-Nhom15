import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * NFR-USE-002/D83 — quét axe-core (WCAG 2.1 A + AA) trên mọi route của SRS 5.1, kể cả trang cần đăng nhập.
 * Đạt = 0 vi phạm, không tắt rule nào (ngoại lệ phải ghi D mới). Phần axe không kiểm được (bàn phím, NVDA, tương
 * phản viền thành phần UI — WCAG 1.4.11) nằm ở docs/a11y-checklist.md.
 *
 * Dữ liệu tự tạo qua API (không dựa vào seed): một Author mới + một recipe đã xuất bản. Riêng /dashboard/categories
 * cần Admin — dùng Admin seed (chỉ có ở Development); không đăng nhập được thì bỏ qua test đó.
 */
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';
const PASSWORD = 'Str0ng!Pass1';
const SEED_ADMIN = { email: 'admin@seed.culinaryblog.local', password: 'Password123!' };
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];
/** `next dev` biên dịch route lần đầu khá lâu. */
const NAVIGATION = { timeout: 30_000 };

interface Fixture {
  email: string;
  recipe: { id: string; slug: string };
  category: { slug: string };
}

/** Recipe do spec tạo — afterAll xóa mềm (D1) để không rác DB dev. Gán ngay sau POST để dọn được cả khi beforeAll fail giữa chừng. */
let createdRecipeId: string | undefined;
let emailUsed = '';
let fixture: Fixture;

async function api<T>(path: string, init: RequestInit & { token?: string } = {}): Promise<T> {
  const { token, headers, ...rest } = init;
  const res = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
  });
  expect(res.ok, `${init.method ?? 'GET'} ${path} → ${res.status}`).toBe(true);
  return (res.status === 204 ? undefined : await res.json()) as T;
}

test.beforeAll(async () => {
  const email = `e2e-a11y-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
  emailUsed = email;
  const { accessToken: token } = await api<{ accessToken: string }>('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password: PASSWORD, displayName: 'Bếp E2E a11y' }),
  });

  const categories = await api<{ id: string; slug: string }[]>('/categories');
  const recipe = await api<{ id: string; slug: string }>('/recipes', {
    method: 'POST',
    token,
    body: JSON.stringify({
      title: `Canh chua a11y ${Date.now()}`,
      description: 'Món canh chua dùng để kiểm tra khả năng tiếp cận.',
      categoryId: categories[0].id,
      prepTime: 15,
      cookTime: 20,
      servings: 4,
      difficulty: 1,
      instructions: null,
      ingredients: [
        { name: 'Cá lóc', quantity: 500, unit: 'g', notes: null },
        { name: 'Me chua', quantity: 50, unit: 'g', notes: null },
      ],
      steps: [
        { title: 'Nấu nước dùng', description: 'Đun sôi nước với me chua.', timerMinutes: 10 },
        { title: 'Cho cá vào', description: 'Thả cá, nêm nếm vừa ăn.', timerMinutes: 5 },
      ],
    }),
  });
  createdRecipeId = recipe.id;
  await api(`/recipes/${recipe.id}/publish`, { method: 'PATCH', token });

  fixture = { email, recipe, category: categories[0] };
});

/** FR-RCP-007/D1 — dọn dữ liệu: đăng nhập lại (access token 15 phút có thể đã hết) rồi xóa mềm recipe đã tạo. */
test.afterAll(async () => {
  if (!createdRecipeId) return;
  const { accessToken: token } = await api<{ accessToken: string }>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email: emailUsed, password: PASSWORD }),
  });
  await api(`/recipes/${createdRecipeId}`, { method: 'DELETE', token });
  createdRecipeId = undefined;
});

/** Chờ hết skeleton (`aria-busy`) rồi mới quét — tránh quét trạng thái đang tải. */
async function expectNoViolations(page: Page, path: string) {
  await test.step(path, async () => {
    const response = await page.goto(path, NAVIGATION);
    expect(response, `mở ${path}`).not.toBeNull();
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0, NAVIGATION);

    const { violations } = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();
    const summary = violations.map(
      (v) => `[${v.impact}] ${v.id}: ${v.help} — ${v.nodes.map((n) => n.target.join(' ')).join(' | ')}`,
    );
    expect(summary, `vi phạm WCAG 2.1 AA ở ${path}`).toEqual([]);
  });
}

async function login(page: Page, email: string, password: string) {
  await page.goto('/auth/login', NAVIGATION);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/, NAVIGATION);
}

test.describe('NFR-USE-002/D83: axe-core WCAG 2.1 AA — 0 vi phạm', () => {
  test('trang công khai (Guest)', async ({ page }) => {
    for (const path of [
      '/',
      '/recipes',
      `/recipes/${fixture.recipe.slug}`,
      '/categories',
      `/categories/${fixture.category.slug}`,
      '/search?q=canh',
      '/search?q=zzkhongtontai',
      '/auth/login',
      '/auth/register',
      '/khong-ton-tai',
    ]) {
      await expectNoViolations(page, path);
    }
  });

  test('trang của Author (dashboard)', async ({ page }) => {
    await login(page, fixture.email, PASSWORD);
    for (const path of [
      '/dashboard',
      '/dashboard/recipes',
      '/dashboard/recipes/new',
      `/dashboard/recipes/${fixture.recipe.id}/edit`,
      '/dashboard/profile',
    ]) {
      await expectNoViolations(page, path);
    }
  });

  test('trang của Admin (/dashboard/categories)', async ({ page }) => {
    try {
      await login(page, SEED_ADMIN.email, SEED_ADMIN.password);
    } catch {
      test.skip(true, 'Không có Admin seed (DbSeeder chỉ chạy ở Development)');
    }
    await expectNoViolations(page, '/dashboard/categories');
  });
});

/**
 * NFR-USE-002 — phần bàn phím axe không kiểm được (WCAG 2.4.3 thứ tự focus, 2.1.1 bàn phím). Chạy trên trình duyệt
 * thật vì jsdom không bỏ focus khi phần tử bị disabled hay bị React di chuyển trong DOM.
 */
test.describe('NFR-USE-002: bàn phím trên trang sửa công thức', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, fixture.email, PASSWORD);
    await page.goto(`/dashboard/recipes/${fixture.recipe.id}/edit`, NAVIGATION);
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0, NAVIGATION);
  });

  test('đổi chỗ bước / nguyên liệu bằng nút Lên/Xuống → focus đi theo mục vừa chuyển, không rơi về trang', async ({
    page,
  }) => {
    const steps = page.getByRole('list', { name: 'Các bước thực hiện' }).getByRole('heading', { level: 3 });
    await page.getByRole('button', { name: 'Chuyển bước 1 xuống' }).focus();
    await page.keyboard.press('Enter');

    await expect(steps).toHaveText(['Bước 1: Cho cá vào', 'Bước 2: Nấu nước dùng']);
    // Bước vừa chuyển giờ ở cuối → nút "xuống" bị khóa, focus sang nút "lên" của chính nó.
    await expect(page.getByRole('button', { name: 'Chuyển bước 2 lên' })).toBeFocused();
    await expect(page.getByRole('list', { name: 'Các bước thực hiện' })).not.toHaveAttribute('aria-busy', 'true');
    await expect(page.getByRole('button', { name: 'Chuyển bước 2 lên' })).toBeFocused();

    await page.getByRole('button', { name: 'Chuyển Cá lóc xuống' }).focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('button', { name: 'Chuyển Cá lóc lên' })).toBeFocused();
    await expect(page.getByRole('list', { name: 'Danh sách nguyên liệu' })).not.toHaveAttribute('aria-busy', 'true');
    await expect(page.getByRole('button', { name: 'Chuyển Cá lóc lên' })).toBeFocused();
  });

  test('hộp xác nhận: Enter mở → focus trong hộp, Esc đóng → focus về nút đã mở', async ({ page }) => {
    const trigger = page.getByRole('button', { name: 'Xóa công thức' });
    await trigger.focus();
    await page.keyboard.press('Enter');

    const dialog = page.getByRole('dialog', { name: 'Xóa công thức này?' });
    await expect(dialog).toBeVisible();
    await expect(dialog.locator(':focus')).toHaveCount(1);

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });
});
