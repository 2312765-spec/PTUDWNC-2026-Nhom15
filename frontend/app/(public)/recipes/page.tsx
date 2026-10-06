import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Skeleton } from '@/components/ui';
import { getCategories } from '@/lib/categories';
import { type RecipeListFilters, toRecipeListQuery } from '@/lib/recipes';
import type { CategoryDto } from '@/lib/types';
import RecipeFilters from './RecipeFilters';
import RecipeResults from './RecipeResults';

/** FR-RCP-001 + FR-SRCH-002/003/004 — SRS 5.1: `/recipes` SSR (dynamic), bộ lọc nằm trên URL. */
export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'Công thức',
  description: 'Khám phá công thức nấu ăn, lọc theo danh mục, độ khó, thời gian nấu và khẩu phần.',
};

type Param = string | string[] | undefined;

interface PageProps {
  searchParams: Promise<Record<string, Param>>;
}

function first(value: Param): string {
  return ((Array.isArray(value) ? value[0] : value) ?? '').trim();
}

export default async function RecipesPage({ searchParams }: PageProps) {
  const params = await searchParams;
  const filters: RecipeListFilters = {
    categoryId: first(params.categoryId),
    difficulty: first(params.difficulty),
    maxCookTime: first(params.maxCookTime),
    minServings: first(params.minServings),
    sort: first(params.sort),
    page: Math.max(1, Number.parseInt(first(params.page), 10) || 1),
  };

  // Bộ lọc danh mục không được làm sập trang: API danh mục lỗi thì vẫn lọc được theo tiêu chí khác.
  const categories = await getCategories().catch((): CategoryDto[] => []);
  const key = toRecipeListQuery(filters).toString();

  return (
    <div className="mx-auto max-w-6xl space-y-6 px-4 py-8">
      <header className="space-y-1 border-b border-border pb-4">
        <h1 className="text-3xl font-bold">Công thức nấu ăn</h1>
        <p className="text-sm text-ink-muted">
          Lọc theo danh mục, độ khó, thời gian nấu và khẩu phần.
        </p>
      </header>

      <RecipeFilters key={key} initial={filters} categories={categories} />

      <Suspense key={key} fallback={<ResultsSkeleton />}>
        <RecipeResults filters={filters} />
      </Suspense>
    </div>
  );
}

function ResultsSkeleton() {
  return (
    <div aria-busy="true" className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: 6 }, (_, i) => (
        <Skeleton key={i} className="h-56" />
      ))}
    </div>
  );
}
