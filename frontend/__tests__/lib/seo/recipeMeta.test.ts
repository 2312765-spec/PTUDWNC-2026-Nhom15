import { buildRecipeDescription, buildRecipeMetadata, buildRecipeTitle } from '@/lib/seo/recipeMeta';
import { recipeDetail } from '../../utils/recipe';

/** Đuôi do template `%s | Culinary Blog` của app/layout.tsx thêm vào. */
const SUFFIX = ' | Culinary Blog';
const SITE = 'https://culinaryblog.vn';

/** Câu văn thật (không lặp một từ) — để kiểm cắt đúng ranh giới từ. */
const LONG_TEXT =
  'Nước dùng ninh xương bò tám tiếng cùng quế hồi thảo quả nướng thơm, bánh phở mềm dai, thịt bò tái ' +
  'chín vừa tới, ăn kèm rau thơm chanh ớt tỏi ngâm giấm. Món sáng quen thuộc của người Hà Nội và ' +
  'nấu được tại nhà với nguyên liệu dễ tìm ở chợ.';

describe('lib/seo/recipeMeta — buildRecipeTitle', () => {
  it('NFR-SEO-002: tên ngắn giữ nguyên, KHÔNG tự ghép đuôi (template của layout đã thêm)', () => {
    expect(buildRecipeTitle('Phở bò Hà Nội')).toBe('Phở bò Hà Nội');
  });

  it('NFR-SEO-002: tên đúng 44 ký tự giữ nguyên → title đầy đủ đúng 60 ký tự', () => {
    const name = 'Bún chả Hà Nội nướng than hoa kèm nem rán ...'.slice(0, 44);
    expect(name).toHaveLength(44);

    const title = buildRecipeTitle(name);

    expect(title).toBe(name);
    expect((title + SUFFIX).length).toBe(60);
  });

  it('NFR-SEO-002: tên dài → cắt tại ranh giới từ + "…", title đầy đủ ≤ 60 ký tự', () => {
    const name = 'Cá kho tộ miền Tây nấu với nước dừa tươi và tiêu xanh đậm đà đưa cơm ngày mưa';

    const title = buildRecipeTitle(name);

    expect((title + SUFFIX).length).toBeLessThanOrEqual(60);
    expect(title.endsWith('…')).toBe(true);
    const kept = title.slice(0, -1);
    expect(name.startsWith(kept)).toBe(true);
    expect(name[kept.length]).toBe(' ');
  });

  it('NFR-SEO-002: bỏ khoảng trắng thừa', () => {
    expect(buildRecipeTitle('  Gỏi   cuốn  ')).toBe('Gỏi cuốn');
  });
});

describe('lib/seo/recipeMeta — buildRecipeDescription (D61.2)', () => {
  it('NFR-SEO-002: mô tả đã 150–160 ký tự → giữ nguyên', () => {
    const description = LONG_TEXT.slice(0, 155);

    expect(buildRecipeDescription(recipeDetail({ description }))).toBe(description.trim());
  });

  it('NFR-SEO-002 / D61: mô tả dài → cắt tại ranh giới từ + "…", còn 150–160 ký tự', () => {
    const result = buildRecipeDescription(recipeDetail({ description: LONG_TEXT }));

    expect(result.length).toBeGreaterThanOrEqual(150);
    expect(result.length).toBeLessThanOrEqual(160);
    expect(result.endsWith('…')).toBe(true);
    const kept = result.slice(0, -1);
    expect(LONG_TEXT.startsWith(kept)).toBe(true);
    expect(LONG_TEXT[kept.length]).toBe(' ');
  });

  it('NFR-SEO-002 / D61: mô tả ngắn → nối câu thời gian, khẩu phần, danh mục; kết quả 150–160 ký tự', () => {
    const description = 'Nước dùng trong, ngọt xương, bánh phở mềm và thịt bò tái chín vừa tới.';

    const result = buildRecipeDescription(recipeDetail({ description }));

    expect(result.startsWith(description)).toBe(true);
    expect(result).toContain('Chuẩn bị 30 phút, nấu 480 phút, 4 phần · Món nước.');
    expect(result.length).toBeGreaterThanOrEqual(150);
    expect(result.length).toBeLessThanOrEqual(160);
  });

  it('D19 / D61: cookTime = 0 → "không cần nấu" thay vì "nấu 0 phút"', () => {
    const result = buildRecipeDescription(recipeDetail({ description: 'Gỏi cuốn tôm thịt.', cookTime: 0, prepTime: 20 }));

    expect(result).toContain('Chuẩn bị 20 phút, không cần nấu, 4 phần · Món nước.');
  });

  it('D61: mô tả rất ngắn vẫn không vượt 160 ký tự và có câu bổ sung', () => {
    const result = buildRecipeDescription(recipeDetail({ description: 'Ngon.' }));

    expect(result.startsWith('Ngon.')).toBe(true);
    expect(result).toContain('Chuẩn bị 30 phút');
    expect(result.length).toBeLessThanOrEqual(160);
  });

  it('NFR-SEO-002: xuống dòng, khoảng trắng liên tiếp gộp thành một dấu cách', () => {
    const description = `${LONG_TEXT.slice(0, 80)}\n\n   ${LONG_TEXT.slice(81, 150)}`;

    expect(buildRecipeDescription(recipeDetail({ description }))).not.toMatch(/\s{2,}|\n/);
  });
});

describe('lib/seo/recipeMeta — buildRecipeMetadata', () => {
  const original = process.env.NEXT_PUBLIC_SITE_URL;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = SITE;
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = original;
  });

  it('NFR-SEO-002: title + description qua hai hàm build', () => {
    const recipe = recipeDetail();

    const metadata = buildRecipeMetadata(recipe);

    expect(metadata.title).toBe(buildRecipeTitle(recipe.title));
    expect(metadata.description).toBe(buildRecipeDescription(recipe));
  });

  it('NFR-SEO-002 / D13 / D61.5: canonical tuyệt đối theo slug', () => {
    expect(buildRecipeMetadata(recipeDetail()).alternates?.canonical).toBe(`${SITE}/recipes/pho-bo-ha-noi`);
  });

  it('NFR-SEO-002: Open Graph đủ og:title, og:description, og:url, og:type, site, locale, ngày', () => {
    const recipe = recipeDetail();

    expect(buildRecipeMetadata(recipe).openGraph).toEqual(
      expect.objectContaining({
        type: 'article',
        url: `${SITE}/recipes/pho-bo-ha-noi`,
        title: 'Phở bò Hà Nội',
        description: buildRecipeDescription(recipe),
        siteName: 'Culinary Blog',
        locale: 'vi_VN',
        publishedTime: '2026-10-01T02:00:00Z',
        modifiedTime: '2026-10-05T09:30:00Z',
        authors: ['Nguyễn Văn An'],
      }),
    );
  });

  it('NFR-SEO-002 / D61.1: og:image 1200×630 trỏ tới URL cố định /og-image (không hash theo build)', () => {
    expect(buildRecipeMetadata(recipeDetail()).openGraph?.images).toEqual([
      { url: `${SITE}/recipes/pho-bo-ha-noi/og-image`, width: 1200, height: 630, alt: expect.any(String) },
    ]);
  });

  it('NFR-SEO-002: Twitter Card summary_large_image dùng chung ảnh OG', () => {
    expect(buildRecipeMetadata(recipeDetail()).twitter).toEqual(
      expect.objectContaining({
        card: 'summary_large_image',
        title: 'Phở bò Hà Nội',
        images: [`${SITE}/recipes/pho-bo-ha-noi/og-image`],
      }),
    );
  });

  it('NFR-SEO-002: Published → index, follow', () => {
    expect(buildRecipeMetadata(recipeDetail({ status: 1 })).robots).toEqual({ index: true, follow: true });
  });

  it.each([
    [0, 'Draft'],
    [2, 'Archived'],
  ])('NFR-SEO-002: status %i (%s) → noindex', (status) => {
    expect(buildRecipeMetadata(recipeDetail({ status })).robots).toEqual({ index: false, follow: false });
  });
});
