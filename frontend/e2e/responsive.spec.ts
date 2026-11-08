import { expect, test, type Page } from '@playwright/test';

/**
 * NFR-USE-001/D84 — responsive. Giả lập thiết bị bằng Playwright (không có BrowserStack — D84.4), bật cảm ứng.
 *  - Mọi route ở 320 / 768 / 1200px: cả trang không cuộn ngang (bảng được cuộn trong khung riêng — D84.3).
 *  - Mọi vùng bấm ≥ 24×24px (D84.2, WCAG 2.5.8); link nằm trong đoạn văn được miễn.
 *  - Lưới card `/recipes`: 1 cột < 768px, 2 cột 768–1199px, 3 cột ≥ 1200px (SRS + D84.1).
 *
 * Dữ liệu tự tạo qua API như a11y.spec.ts; /dashboard/categories dùng Admin seed, không có thì bỏ qua.
 */
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';
const PASSWORD = 'Str0ng!Pass1';
const SEED_ADMIN = { email: 'admin@seed.culinaryblog.local', password: 'Password123!' };
const WIDTHS = [320, 768, 1200];
const MIN_TARGET = 24;
const NAVIGATION = { timeout: 30_000 };

test.use({ hasTouch: true });

let fixture: { email: string; recipe: { id: string; slug: string }; categorySlug: string };

async function api<T>(path: string, init: RequestInit & { token?: string } = {}): Promise<T> {
  const { token, headers, ...rest } = init;
  const res = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers },
  });
  expect(res.ok, `${init.method ?? 'GET'} ${path} → ${res.status}`).toBe(true);
  return (res.status === 204 ? undefined : await res.json()) as T;
}

test.beforeAll(async () => {
  const email = `e2e-resp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
  const { accessToken: token } = await api<{ accessToken: string }>('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password: PASSWORD, displayName: 'Bếp E2E responsive' }),
  });
  const categories = await api<{ id: string; slug: string }[]>('/categories');
  const recipe = await api<{ id: string; slug: string }>('/recipes', {
    method: 'POST',
    token,
    body: JSON.stringify({
      title: `Gỏi cuốn responsive ${Date.now()}`,
      description: 'Kiểm tra bố cục ở nhiều kích thước màn hình.',
      categoryId: categories[0].id,
      prepTime: 20,
      cookTime: 0,
      servings: 4,
      difficulty: 1,
      instructions: null,
      ingredients: [
        { name: 'Bánh tráng', quantity: 20, unit: 'cái', notes: null },
        { name: 'Tôm', quantity: 300, unit: 'g', notes: 'luộc chín, chẻ đôi' },
      ],
      steps: [
        { title: 'Sơ chế', description: 'Rửa rau, luộc tôm và thịt ba chỉ cho chín đều.', timerMinutes: 15 },
        { title: 'Cuốn', description: 'Nhúng bánh tráng, xếp rau, tôm, thịt rồi cuốn chặt tay.', timerMinutes: null },
      ],
    }),
  });
  await api(`/recipes/${recipe.id}/publish`, { method: 'PATCH', token });
  fixture = { email, recipe, categorySlug: categories[0].slug };
});

async function login(page: Page, email: string, password: string) {
  await page.goto('/auth/login', NAVIGATION);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/, NAVIGATION);
}

/** Đo ở một độ rộng: phần tử gây tràn ngang + vùng bấm < 24px. */
function measure(page: Page) {
  return page.evaluate((min) => {
    const vw = window.innerWidth;
    const overflowBy = document.documentElement.scrollWidth - vw;
    const culprits: string[] = [];
    if (overflowBy > 0) {
      for (const el of document.querySelectorAll('body *')) {
        const r = el.getBoundingClientRect();
        const parent = el.parentElement?.getBoundingClientRect();
        if (r.width && r.right > vw + 1 && !(parent && parent.right > vw + 1)) {
          culprits.push(`<${el.tagName.toLowerCase()} class="${String(el.className).slice(0, 60)}">`);
        }
      }
    }
    const small: string[] = [];
    for (const el of document.querySelectorAll<HTMLElement>(
      'a[href], button, input:not([type=hidden]), select, textarea, [role=tab]',
    )) {
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      if (!r.width || !r.height || cs.visibility === 'hidden') continue;
      if (el.closest('nextjs-portal, .sr-only') || el.classList.contains('sr-only')) continue; // dev overlay / ẩn
      if (el.tagName === 'A' && cs.display === 'inline' && el.closest('p')) continue; // link trong đoạn văn — miễn
      if (r.width < min || r.height < min) {
        const name = (el.getAttribute('aria-label') || el.innerText || el.tagName).trim().replace(/\s+/g, ' ');
        small.push(`"${name.slice(0, 30)}" ${Math.round(r.width)}×${Math.round(r.height)}`);
      }
    }
    return { overflowBy, culprits: culprits.slice(0, 3), small: [...new Set(small)] };
  }, MIN_TARGET);
}

async function checkRoute(page: Page, path: string) {
  for (const width of WIDTHS) {
    await test.step(`${path} @${width}px`, async () => {
      await page.setViewportSize({ width, height: 900 });
      await page.goto(path, NAVIGATION);
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0, NAVIGATION);
      const { overflowBy, culprits, small } = await measure(page);
      expect(overflowBy, `${path} @${width}px tràn ngang do ${culprits.join(', ')}`).toBeLessThanOrEqual(0);
      expect(small, `${path} @${width}px: vùng bấm < ${MIN_TARGET}px`).toEqual([]);
    });
  }
}

test.describe('NFR-USE-001/D84: không cuộn ngang, vùng bấm ≥ 24px ở 320 / 768 / 1200px', () => {
  test('trang công khai (Guest)', async ({ page }) => {
    for (const path of [
      '/',
      '/recipes',
      `/recipes/${fixture.recipe.slug}`,
      '/categories',
      `/categories/${fixture.categorySlug}`,
      '/search?q=gỏi',
      '/auth/login',
      '/auth/register',
    ]) {
      await checkRoute(page, path);
    }
  });

  test('trang của Author', async ({ page }) => {
    await login(page, fixture.email, PASSWORD);
    for (const path of [
      '/dashboard',
      '/dashboard/recipes',
      '/dashboard/recipes/new',
      `/dashboard/recipes/${fixture.recipe.id}/edit`,
      '/dashboard/profile',
    ]) {
      await checkRoute(page, path);
    }
  });

  test('trang của Admin (/dashboard/categories)', async ({ page }) => {
    try {
      await login(page, SEED_ADMIN.email, SEED_ADMIN.password);
    } catch {
      test.skip(true, 'Không có Admin seed (DbSeeder chỉ chạy ở Development)');
    }
    await checkRoute(page, '/dashboard/categories');
  });
});

test('NFR-USE-001/D84: lưới /recipes — 1 cột mobile, 2 cột tablet (768–1199px), 3 cột desktop (≥ 1200px)', async ({
  page,
}) => {
  for (const [width, columns] of [
    [320, 1],
    [767, 1],
    [768, 2],
    [1024, 2],
    [1199, 2],
    [1200, 3],
  ] as const) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/recipes', NAVIGATION);
    const cards = page.getByRole('region', { name: 'Danh sách công thức' }).getByRole('listitem');
    await expect(cards.first()).toBeVisible(NAVIGATION);
    const tops = await cards.evaluateAll((items) => items.map((li) => Math.round(li.getBoundingClientRect().top)));
    const firstRow = tops.filter((top) => Math.abs(top - tops[0]) < 2).length;
    expect(firstRow, `số cột @${width}px`).toBe(Math.min(columns, tops.length));
  }
});
