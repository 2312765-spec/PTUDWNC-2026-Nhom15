import { buildRecipeJsonLd } from '@/lib/seo/recipeJsonLd';
import { recipeDetail } from '../../utils/recipe';

const SITE = 'https://culinaryblog.vn';
const URL = `${SITE}/recipes/pho-bo-ha-noi`;

/** NFR-SEO-001 — 12 thuộc tính SRS §4.7 liệt kê. */
const SRS_PROPERTIES = [
  'name',
  'description',
  'image',
  'author',
  'datePublished',
  'prepTime',
  'cookTime',
  'totalTime',
  'recipeYield',
  'recipeIngredient',
  'recipeInstructions',
  'nutrition',
];

describe('lib/seo/recipeJsonLd — buildRecipeJsonLd', () => {
  it('NFR-SEO-001: @context schema.org, @type Recipe, đủ 12 thuộc tính SRS', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail(), SITE);

    expect(jsonLd['@context']).toBe('https://schema.org');
    expect(jsonLd['@type']).toBe('Recipe');
    for (const property of SRS_PROPERTIES) {
      expect(jsonLd).toHaveProperty(property);
    }
  });

  it('NFR-SEO-001: name, description, url canonical, recipeCategory', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail(), SITE);

    expect(jsonLd).toEqual(
      expect.objectContaining({
        name: 'Phở bò Hà Nội',
        description: recipeDetail().description,
        url: URL,
        recipeCategory: 'Món nước',
      }),
    );
  });

  it('NFR-SEO-001: author là Person theo displayName (D5)', () => {
    expect(buildRecipeJsonLd(recipeDetail(), SITE).author).toEqual({ '@type': 'Person', name: 'Nguyễn Văn An' });
  });

  it('NFR-SEO-001: datePublished = publishedAt, dateModified = updatedAt', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail(), SITE);

    expect(jsonLd.datePublished).toBe('2026-10-01T02:00:00Z');
    expect(jsonLd.dateModified).toBe('2026-10-05T09:30:00Z');
  });

  it('NFR-SEO-001: chưa publish (Draft) → datePublished lấy createdAt, không bỏ trống', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail({ status: 0, publishedAt: undefined, updatedAt: undefined }), SITE);

    expect(jsonLd.datePublished).toBe('2026-09-30T08:00:00Z');
    expect(jsonLd).not.toHaveProperty('dateModified');
  });

  it('NFR-SEO-001: thời gian dạng ISO 8601 duration, totalTime = prep + cook', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail(), SITE);

    expect(jsonLd.prepTime).toBe('PT30M');
    expect(jsonLd.cookTime).toBe('PT480M');
    expect(jsonLd.totalTime).toBe('PT510M');
  });

  it('D19: cookTime = 0 hợp lệ → PT0M', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail({ prepTime: 20, cookTime: 0 }), SITE);

    expect(jsonLd.cookTime).toBe('PT0M');
    expect(jsonLd.totalTime).toBe('PT20M');
  });

  it('NFR-SEO-001: recipeYield theo servings', () => {
    expect(buildRecipeJsonLd(recipeDetail(), SITE).recipeYield).toBe('4 phần');
  });

  it('NFR-SEO-001 / D61.1: image = ảnh chính trước, ảnh khác sau, cuối cùng ảnh OG 1200×630 — đều tuyệt đối', () => {
    expect(buildRecipeJsonLd(recipeDetail(), SITE).image).toEqual([
      'https://culinary.example/culinary-blog/recipes/x/a.jpg',
      'https://culinary.example/culinary-blog/recipes/x/b.jpg',
      `${URL}/og-image`,
    ]);
  });

  it('NFR-SEO-001: không có ảnh → vẫn có image (ảnh OG), không dùng đường dẫn tương đối /placeholder', () => {
    const image = buildRecipeJsonLd(recipeDetail({ images: [] }), SITE).image;

    expect(image).toEqual([`${URL}/og-image`]);
  });

  it('NFR-SEO-001: recipeIngredient theo orderIndex, gồm số lượng, đơn vị, ghi chú; "vừa đủ" chỉ có tên (D7)', () => {
    expect(buildRecipeJsonLd(recipeDetail(), SITE).recipeIngredient).toEqual([
      '1.5 kg xương bò (chần sơ)',
      '500 g bánh phở',
      'hành lá',
    ]);
  });

  it('NFR-SEO-001: recipeInstructions là HowToStep theo stepNumber, có position, url neo #buoc-n, ảnh bước', () => {
    expect(buildRecipeJsonLd(recipeDetail(), SITE).recipeInstructions).toEqual([
      {
        '@type': 'HowToStep',
        position: 1,
        name: 'Sơ chế xương',
        text: 'Chần xương bò qua nước sôi rồi rửa sạch.',
        url: `${URL}#buoc-1`,
        image: 'https://culinary.example/culinary-blog/recipes/x/step1.jpg',
      },
      {
        '@type': 'HowToStep',
        position: 2,
        name: 'Ninh nước dùng',
        text: 'Ninh xương 8 tiếng với quế, hồi.',
        url: `${URL}#buoc-2`,
      },
    ]);
  });

  it('NFR-SEO-001: nutrition là NutritionInformation, đủ 6 chỉ số có đơn vị', () => {
    expect(buildRecipeJsonLd(recipeDetail(), SITE).nutrition).toEqual({
      '@type': 'NutritionInformation',
      calories: '450 calories',
      proteinContent: '30 g',
      carbohydrateContent: '55 g',
      fatContent: '12 g',
      fiberContent: '3 g',
      sodiumContent: '1200 mg',
    });
  });

  it('NFR-SEO-001: chỉ số bằng 0 vẫn giữ, chỉ số null thì bỏ', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail({ nutrition: { calories: 0, protein: undefined } }), SITE);

    expect(jsonLd.nutrition).toEqual({ '@type': 'NutritionInformation', calories: '0 calories' });
  });

  it('NFR-SEO-001: không có số liệu dinh dưỡng → bỏ hẳn nutrition (Google báo lỗi khi object rỗng)', () => {
    expect(buildRecipeJsonLd(recipeDetail({ nutrition: undefined }), SITE)).not.toHaveProperty('nutrition');
    expect(buildRecipeJsonLd(recipeDetail({ nutrition: {} }), SITE)).not.toHaveProperty('nutrition');
  });

  it('NFR-SEO-001: không có trường undefined lọt vào output', () => {
    const jsonLd = buildRecipeJsonLd(recipeDetail({ nutrition: undefined, updatedAt: undefined }), SITE);

    expect(JSON.parse(JSON.stringify(jsonLd))).toEqual(jsonLd);
    expect(Object.values(jsonLd)).not.toContain(undefined);
  });
});
