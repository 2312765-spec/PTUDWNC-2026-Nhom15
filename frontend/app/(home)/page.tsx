import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { RecipeMenuRow } from '@/components/recipes/RecipeMenuRow';
import { RecipeImagePlaceholder } from '@/components/recipes/RecipeImagePlaceholder';
import { getLatestRecipes } from '@/lib/recipes';
import { difficultyLabel, formatMinutes } from '@/lib/recipe-format';
import type { RecipeSummaryDto } from '@/lib/types';

/**
 * Trang chủ — chủ sở hữu: B. FR-RCP-001 (công thức mới nhất). SRS mục 5.1: ISR revalidate = 3600 — mọi fetch ở đây
 * phải dùng `revalidate`, không `no-store`.
 *
 * D97 — bố cục "sổ tay nấu ăn": món mới nhất là "Món đáng thử", các món sau là "Thực đơn mới" (danh sách có đường
 * chấm dẫn). Danh mục (FR-CAT-001) và ô tìm kiếm (FR-SRCH-001) nằm ở thanh bên của layout gốc.
 */
export const revalidate = 3600;

/** NFR-SEO-002 — title/description mặc định lấy từ layout; canonical tương đối theo `metadataBase`. */
export const metadata: Metadata = {
  alternates: { canonical: '/' },
};

const LATEST_RECIPES = 6;

/** Lỗi không làm sập trang (NFR-USE-003) — `null` nghĩa là tải lỗi, khác danh sách rỗng. */
async function latestOrNull(): Promise<RecipeSummaryDto[] | null> {
  try {
    return (await getLatestRecipes(LATEST_RECIPES)).items;
  } catch {
    return null;
  }
}

function FeaturedRecipe({ recipe }: { recipe: RecipeSummaryDto }) {
  const href = `/recipes/${recipe.slug}`;
  return (
    <section
      aria-labelledby="home-featured-label"
      className="flex flex-wrap overflow-hidden rounded-3xl border border-border bg-surface"
    >
      <div className="relative min-h-60 flex-[3_1_360px] bg-brand-50 md:min-h-80">
        {recipe.featuredImageUrl ? (
          // NFR-PERF-005/D86 — ảnh này là LCP của trang chủ: preload trong <head>.
          <Image
            src={recipe.featuredImageUrl}
            alt={recipe.title}
            fill
            sizes="(min-width: 1200px) 560px, 100vw"
            preload
            className="object-cover"
          />
        ) : (
          <RecipeImagePlaceholder title={recipe.title} slug={recipe.slug} size="hero" className="absolute inset-0" />
        )}
      </div>
      <div className="flex flex-[2_1_280px] flex-col justify-center gap-3 p-6 md:p-8">
        <p id="home-featured-label" className="text-xs font-bold uppercase tracking-[0.18em] text-accent">
          Món đáng thử
        </p>
        <h2 className="break-words text-4xl font-extrabold leading-none">{recipe.title}</h2>
        {recipe.description && <p className="leading-relaxed text-ink-muted">{recipe.description}</p>}
        <p className="text-sm text-ink-muted">
          {formatMinutes(recipe.prepTimeMinutes + recipe.cookTimeMinutes)} · {difficultyLabel(recipe.difficulty)}
          {recipe.authorName && ` · ${recipe.authorName}`}
        </p>
        <Link
          href={href}
          aria-label={`Xem công thức ${recipe.title}`}
          className="mt-2 inline-flex h-11 items-center self-start rounded-lg bg-brand-600 px-5 font-bold text-on-brand hover:bg-brand-700"
        >
          Xem công thức
        </Link>
      </div>
    </section>
  );
}

export default async function HomePage() {
  const recipes = await latestOrNull();
  const [featured, ...rest] = recipes ?? [];

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-10 px-4 py-8 md:gap-12 md:px-8 md:py-10 lg:px-12">
      <h1 className="text-4xl font-extrabold leading-none md:text-6xl">
        Hôm nay nấu <span className="text-brand-600">gì</span> đây?
      </h1>

      {recipes === null ? (
        <p role="alert" className="rounded-xl border border-danger-line bg-danger-subtle p-6 text-center text-sm text-danger">
          Không tải được công thức. Vui lòng thử lại sau.
        </p>
      ) : !featured ? (
        <p className="rounded-xl border border-dashed border-border bg-surface p-12 text-center text-ink-muted">
          Chưa có công thức nào được đăng tải.
        </p>
      ) : (
        <>
          <FeaturedRecipe recipe={featured} />

          {rest.length > 0 && (
            <section aria-labelledby="home-menu">
              <div className="flex flex-wrap items-baseline justify-between gap-3 border-b-2 border-ink pb-3">
                <h2 id="home-menu" className="text-3xl font-extrabold leading-none md:text-4xl">
                  Thực đơn mới
                </h2>
                <Link href="/recipes" className="inline-flex min-h-6 items-center font-semibold text-accent hover:underline">
                  Xem cả thực đơn
                </Link>
              </div>
              <ol>
                {rest.map((recipe, index) => (
                  <RecipeMenuRow key={recipe.id} recipe={recipe} index={index} />
                ))}
              </ol>
            </section>
          )}
        </>
      )}
    </div>
  );
}
