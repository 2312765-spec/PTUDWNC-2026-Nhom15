import type { RecipeDetailDto } from '@/lib/types';
import { recipeOgImagePath } from './ogImage';

type JsonLd = Record<string, unknown>;
type Nutrition = NonNullable<RecipeDetailDto['nutrition']>;

/** Schema.org NutritionInformation: tên thuộc tính + đơn vị cho từng chỉ số của DTO. */
const NUTRITION_FIELDS: Array<[keyof Nutrition, string, string]> = [
  ['calories', 'calories', 'calories'],
  ['protein', 'proteinContent', 'g'],
  ['carbohydrates', 'carbohydrateContent', 'g'],
  ['fat', 'fatContent', 'g'],
  ['fiber', 'fiberContent', 'g'],
  ['sodium', 'sodiumContent', 'mg'],
];

function duration(minutes: number): string {
  return `PT${minutes}M`;
}

/** Chỉ số bằng 0 vẫn giữ; `null`/`undefined` bỏ. Không còn chỉ số nào → bỏ hẳn (object rỗng Google báo lỗi). */
function buildNutrition(nutrition: RecipeDetailDto['nutrition']): JsonLd | undefined {
  if (!nutrition) return undefined;
  const entries = NUTRITION_FIELDS.filter(([key]) => nutrition[key] != null).map(
    ([key, property, unit]) => [property, `${nutrition[key]} ${unit}`] as const,
  );
  return entries.length > 0 ? { '@type': 'NutritionInformation', ...Object.fromEntries(entries) } : undefined;
}

/** D7 — nguyên liệu "vừa đủ" (`quantity`/`unit` null) chỉ còn tên. */
function formatIngredient(ingredient: RecipeDetailDto['ingredients'][number]): string {
  const text = [ingredient.quantity, ingredient.unit, ingredient.name].filter((part) => part != null && part !== '').join(' ');
  return ingredient.notes ? `${text} (${ingredient.notes})` : text;
}

/**
 * NFR-SEO-001 — JSON-LD Schema.org `Recipe` cho `/recipes/{slug}`. Mọi URL tuyệt đối; `image` luôn có
 * ít nhất ảnh OG 1200×630 (D61.1) để Rich Results không báo thiếu ảnh khi công thức chưa có ảnh nào.
 */
export function buildRecipeJsonLd(recipe: RecipeDetailDto, siteUrl: string): JsonLd {
  const url = `${siteUrl}/recipes/${recipe.slug}`;
  const images = [...recipe.images]
    .sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary) || a.orderIndex - b.orderIndex)
    .map((image) => image.originalUrl);
  const nutrition = buildNutrition(recipe.nutrition);

  return {
    '@context': 'https://schema.org',
    '@type': 'Recipe',
    name: recipe.title,
    description: recipe.description,
    url,
    image: [...images, `${siteUrl}${recipeOgImagePath(recipe.slug)}`],
    author: { '@type': 'Person', name: recipe.author.displayName },
    datePublished: recipe.publishedAt ?? recipe.createdAt,
    ...(recipe.updatedAt ? { dateModified: recipe.updatedAt } : {}),
    prepTime: duration(recipe.prepTime),
    cookTime: duration(recipe.cookTime), // D19: 0 hợp lệ → PT0M
    totalTime: duration(recipe.prepTime + recipe.cookTime),
    recipeYield: `${recipe.servings} phần`,
    recipeCategory: recipe.category.name,
    recipeIngredient: [...recipe.ingredients].sort((a, b) => a.orderIndex - b.orderIndex).map(formatIngredient),
    recipeInstructions: [...recipe.steps]
      .sort((a, b) => a.stepNumber - b.stepNumber)
      .map((step) => ({
        '@type': 'HowToStep',
        position: step.stepNumber,
        name: step.title,
        text: step.description,
        url: `${url}#buoc-${step.stepNumber}`,
        ...(step.imageUrl ? { image: step.imageUrl } : {}),
      })),
    ...(nutrition ? { nutrition } : {}),
  };
}
