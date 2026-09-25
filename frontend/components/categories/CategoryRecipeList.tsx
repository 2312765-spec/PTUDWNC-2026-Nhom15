'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Badge, Button, Card, CardBody, Pagination, Skeleton } from '@/components/ui';
import { ApiError, getCategoryBySlug } from '@/lib/categories';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';

interface Props {
  slug: string;
  pageSize: number;
  /** Trang 1 đã render sẵn ở server (ISR 600) — chỉ gọi API khi người dùng sang trang khác. */
  initialRecipes: PagedResult<RecipeSummaryDto>;
}

const DIFFICULTY_LABEL: Record<string, string> = {
  Easy: 'Dễ',
  Medium: 'Trung bình',
  Hard: 'Khó',
  Expert: 'Rất khó',
};

/**
 * FR-CAT-002 — phân trang phía client. Không dùng `searchParams` ở page vì nó ép route thành
 * dynamic, mất ISR 600 theo SRS 5.1.
 */
export default function CategoryRecipeList({ slug, pageSize, initialRecipes }: Props) {
  const [page, setPage] = useState(1);

  const { data, isFetching, isError, refetch } = useQuery({
    queryKey: ['category-recipes', slug, pageSize, page],
    queryFn: async () => {
      const result = await getCategoryBySlug(slug, page, pageSize);
      if (!result) throw new ApiError(404, 'CATEGORY_NOT_FOUND');
      return result.recipes;
    },
    initialData: page === 1 ? initialRecipes : undefined,
    placeholderData: keepPreviousData,
  });

  if (isError) {
    return (
      <Card>
        <CardBody role="alert" className="flex flex-col items-center gap-3 py-8 text-center">
          <p className="text-ink-muted">Không tải được danh sách công thức.</p>
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            Thử lại
          </Button>
        </CardBody>
      </Card>
    );
  }

  if (!data) {
    return <RecipeListSkeleton count={pageSize} />;
  }

  if (data.items.length === 0) {
    return (
      <Card>
        <CardBody className="py-10 text-center text-ink-muted">
          Chưa có công thức nào được xuất bản trong danh mục này.
        </CardBody>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <ul
        aria-busy={isFetching}
        className={`grid grid-cols-1 gap-4 sm:grid-cols-2 ${isFetching ? 'opacity-60' : ''}`}
      >
        {data.items.map((recipe) => (
          // TODO(S4 — B): bọc <Link href={`/recipes/${recipe.slug}`}> khi có trang chi tiết công thức.
          <li key={recipe.id}>
            <Card className="h-full">
              <CardBody className="flex h-full flex-col gap-2">
                <div className="flex items-center justify-between gap-2 text-xs">
                  <Badge variant="warning">
                    {DIFFICULTY_LABEL[recipe.difficulty] ?? recipe.difficulty}
                  </Badge>
                  <span className="text-ink-muted">
                    {recipe.prepTimeMinutes + recipe.cookTimeMinutes} phút
                  </span>
                </div>
                <h3 className="font-semibold">{recipe.title}</h3>
                {recipe.description && (
                  <p className="line-clamp-2 text-sm text-ink-muted">{recipe.description}</p>
                )}
                <div className="mt-auto flex items-center justify-between gap-2 pt-2 text-xs text-ink-muted">
                  <span>Tác giả: {recipe.authorName ?? 'Ẩn danh'}</span>
                  {recipe.status !== 'Published' && <Badge>Nháp</Badge>}
                </div>
              </CardBody>
            </Card>
          </li>
        ))}
      </ul>

      <Pagination page={data.page} totalPages={data.totalPages} onPageChange={setPage} />
    </div>
  );
}

function RecipeListSkeleton({ count }: { count: number }) {
  return (
    <div aria-busy="true" className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      {Array.from({ length: Math.min(count, 4) }, (_, i) => (
        <Skeleton key={i} className="h-32" />
      ))}
    </div>
  );
}
