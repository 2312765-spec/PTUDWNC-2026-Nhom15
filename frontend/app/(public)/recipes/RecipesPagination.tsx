'use client';

import { useRouter } from 'next/navigation';
import { Pagination } from '@/components/ui';
import { type RecipeListFilters, toRecipeListQuery } from '@/lib/recipes';

/** FR-SRCH-004 — đổi trang bằng URL, giữ nguyên bộ lọc/sắp xếp hiện tại. */
export default function RecipesPagination({
  filters,
  page,
  totalPages,
}: {
  filters: RecipeListFilters;
  page: number;
  totalPages: number;
}) {
  const router = useRouter();

  return (
    <Pagination
      page={page}
      totalPages={totalPages}
      onPageChange={(p) => {
        const query = toRecipeListQuery({ ...filters, page: p }).toString();
        router.push(query ? `/recipes?${query}` : '/recipes');
      }}
    />
  );
}
