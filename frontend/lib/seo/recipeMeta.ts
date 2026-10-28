import type { Metadata } from 'next';
import type { RecipeDetailDto } from '@/lib/types';
import { OG_IMAGE_ALT, OG_IMAGE_SIZE, recipeOgImagePath } from './ogImage';
import { absoluteUrl, SITE_NAME } from './site';

/** Đuôi `" | Culinary Blog"` do template của app/layout.tsx thêm — 16 ký tự. */
const TITLE_SUFFIX_LENGTH = ` | ${SITE_NAME}`.length;
const TITLE_MAX_LENGTH = 60;
const DESCRIPTION_MIN_LENGTH = 150;
const DESCRIPTION_MAX_LENGTH = 160;
const PUBLISHED = 1;

function collapseWhitespace(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

/** Cắt tại ranh giới từ để kết quả (kể cả "…") không vượt `max` ký tự. */
function truncateAtWord(text: string, max: number): string {
  if (text.length <= max) return text;
  const cut = text.slice(0, max); // max - 1 ký tự + vị trí kế tiếp để biết có đang ở giữa từ
  const lastSpace = cut.lastIndexOf(' ');
  const kept = lastSpace > 0 ? cut.slice(0, lastSpace) : cut.slice(0, max - 1);
  return `${kept.replace(/[\s,;:·–-]+$/, '')}…`;
}

/** NFR-SEO-002 — phần tên món của `<title>`; template layout thêm đuôi, tổng ≤ 60 ký tự. */
export function buildRecipeTitle(title: string): string {
  return truncateAtWord(collapseWhitespace(title), TITLE_MAX_LENGTH - TITLE_SUFFIX_LENGTH);
}

type DescriptionSource = Pick<RecipeDetailDto, 'description' | 'prepTime' | 'cookTime' | 'servings' | 'category'>;

/**
 * NFR-SEO-002 / D61.2 — meta description 150–160 ký tự. Ngắn thì nối thông tin thời gian, khẩu phần,
 * danh mục (rồi một câu giới thiệu nếu vẫn thiếu); dài thì cắt tại ranh giới từ.
 */
export function buildRecipeDescription(recipe: DescriptionSource): string {
  let text = collapseWhitespace(recipe.description);
  if (text.length <= DESCRIPTION_MAX_LENGTH && text.length >= DESCRIPTION_MIN_LENGTH) return text;

  if (text.length < DESCRIPTION_MIN_LENGTH) {
    const cook = recipe.cookTime > 0 ? `nấu ${recipe.cookTime} phút` : 'không cần nấu'; // D19
    const facts = `Chuẩn bị ${recipe.prepTime} phút, ${cook}, ${recipe.servings} phần · ${recipe.category.name}.`;
    text = collapseWhitespace(`${text} ${facts}`);
    if (text.length < DESCRIPTION_MIN_LENGTH) {
      text = `${text} Xem nguyên liệu và hướng dẫn từng bước trên ${SITE_NAME}.`;
    }
  }

  return truncateAtWord(text, DESCRIPTION_MAX_LENGTH);
}

/** NFR-SEO-002 — metadata trang `/recipes/{slug}`. og:image do Route Handler `og-image` sinh (D61.1). */
export function buildRecipeMetadata(recipe: RecipeDetailDto): Metadata {
  const url = absoluteUrl(`/recipes/${recipe.slug}`); // D13: slug bất biến → canonical ổn định
  const description = buildRecipeDescription(recipe);
  const indexable = recipe.status === PUBLISHED;
  const image = { url: absoluteUrl(recipeOgImagePath(recipe.slug)), ...OG_IMAGE_SIZE, alt: OG_IMAGE_ALT };

  return {
    title: buildRecipeTitle(recipe.title),
    description,
    alternates: { canonical: url },
    // Metadata merge nông: openGraph ở đây thay hẳn openGraph của layout → khai báo lại siteName/locale.
    openGraph: {
      type: 'article',
      url,
      title: recipe.title,
      description,
      siteName: SITE_NAME,
      locale: 'vi_VN',
      publishedTime: recipe.publishedAt ?? recipe.createdAt,
      ...(recipe.updatedAt ? { modifiedTime: recipe.updatedAt } : {}),
      authors: [recipe.author.displayName],
      images: [image],
    },
    twitter: { card: 'summary_large_image', title: recipe.title, description, images: [image.url] },
    // NFR-SEO-002: Draft/Archived không cho index
    robots: indexable ? { index: true, follow: true } : { index: false, follow: false },
  };
}
