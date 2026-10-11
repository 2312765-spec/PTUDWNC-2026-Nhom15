import Link from 'next/link';
import RecipeCard from '@/components/recipes/RecipeCard';
import { Button, Input } from '@/components/ui';
import { getCategories } from '@/lib/categories';
import { getLatestRecipes, SEARCH_MIN_LENGTH } from '@/lib/recipes';
import type { CategoryDto, RecipeSummaryDto } from '@/lib/types';

/**
 * Trang chủ — chủ sở hữu: B. FR-RCP-001 (công thức mới nhất) + FR-CAT-001 (danh mục) +
 * lối vào FR-SRCH-001. SRS mục 5.1: ISR revalidate = 3600 — mọi fetch ở đây phải dùng
 * `revalidate`, không `no-store`.
 */
export const revalidate = 3600;

const LATEST_RECIPES = 6;
const FEATURED_CATEGORIES = 6;

/** Lỗi từng phần không làm sập trang (NFR-USE-003) — `null` nghĩa là tải lỗi, khác danh sách rỗng. */
async function orNull<T>(promise: Promise<T>): Promise<T | null> {
  try {
    return await promise;
  } catch {
    return null;
  }
}

export default async function HomePage() {
  const [categories, recipes] = await Promise.all([
    orNull<CategoryDto[]>(getCategories()),
    orNull(getLatestRecipes(LATEST_RECIPES)).then((r): RecipeSummaryDto[] | null => r?.items ?? null),
  ]);

  return (
    <div>
      {/* Hero + tìm kiếm nhanh (FR-SRCH-001). Form GET thuần: chạy cả khi chưa có JS. */}
      <section className="border-b border-border bg-brand-50 py-16 md:py-24">
        <div className="mx-auto max-w-5xl px-4 text-center">
          <h1 className="text-4xl font-extrabold tracking-tight text-ink md:text-5xl">
            Nấu ngon mỗi ngày cùng <span className="text-brand-700">Culinary Blog</span>
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-base text-ink-muted md:text-lg">
            Công thức nấu ăn từ các đầu bếp gia đình và chuyên nghiệp.
          </p>

          <form role="search" action="/search" method="GET" className="mx-auto mt-8 flex max-w-xl items-start gap-2">
            <div className="flex-1">
              <Input
                type="search"
                name="q"
                aria-label="Tìm công thức"
                placeholder={`Tìm theo tên hoặc mô tả món ăn… (tối thiểu ${SEARCH_MIN_LENGTH} ký tự)`}
                minLength={SEARCH_MIN_LENGTH}
                required
              />
            </div>
            <Button type="submit">Tìm kiếm</Button>
          </form>
        </div>
      </section>

      <div className="mx-auto max-w-6xl space-y-16 px-4 py-12">
        {/* Danh mục (FR-CAT-001) */}
        <section aria-labelledby="home-categories">
          <div className="mb-6 flex items-center justify-between">
            <h2 id="home-categories" className="text-2xl font-bold text-ink">
              Danh mục món ăn
            </h2>
            <Link href="/categories" className="text-sm font-semibold text-brand-700 hover:underline">
              Xem tất cả →
            </Link>
          </div>

          {categories === null ? (
            <p role="alert" className="rounded-lg border border-border bg-surface p-6 text-center text-sm text-red-700">
              Không tải được danh mục. Vui lòng thử lại sau.
            </p>
          ) : (
            <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
              {categories.slice(0, FEATURED_CATEGORIES).map((cat) => (
                <li key={cat.id}>
                  <Link
                    href={`/categories/${cat.slug}`}
                    className="flex min-h-24 flex-col items-center justify-center rounded-lg border border-border bg-surface p-4 text-center transition hover:shadow-md focus-visible:outline-2 focus-visible:outline-brand-600"
                  >
                    <span className="text-sm font-semibold text-ink">{cat.name}</span>
                    <span className="mt-1 text-xs text-ink-muted">{cat.recipeCount} công thức</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Công thức mới nhất (FR-RCP-001) */}
        <section aria-labelledby="home-latest">
          <div className="mb-6 flex items-center justify-between">
            <h2 id="home-latest" className="text-2xl font-bold text-ink">
              Công thức mới nhất
            </h2>
            <Link href="/recipes" className="text-sm font-semibold text-brand-700 hover:underline">
              Khám phá thêm →
            </Link>
          </div>

          {recipes === null ? (
            <p role="alert" className="rounded-lg border border-border bg-surface p-6 text-center text-sm text-red-700">
              Không tải được công thức. Vui lòng thử lại sau.
            </p>
          ) : recipes.length === 0 ? (
            <p className="rounded-lg border border-dashed border-border bg-surface p-12 text-center text-ink-muted">
              Chưa có công thức nào được đăng tải.
            </p>
          ) : (
            <ul className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
              {recipes.map((recipe) => (
                <li key={recipe.id}>
                  <RecipeCard recipe={recipe} />
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
