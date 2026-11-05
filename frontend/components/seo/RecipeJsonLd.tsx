import { serializeJsonLd } from '@/lib/seo/jsonLd';
import { buildRecipeJsonLd } from '@/lib/seo/recipeJsonLd';
import { getSiteUrl } from '@/lib/seo/site';
import type { RecipeDetailDto } from '@/lib/types';

/** NFR-SEO-001 — JSON-LD Schema.org Recipe. `serializeJsonLd` escape `<` chống stored XSS. */
export function RecipeJsonLd({ recipe }: { recipe: RecipeDetailDto }) {
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: serializeJsonLd(buildRecipeJsonLd(recipe, getSiteUrl())) }}
    />
  );
}
