'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { type FormEvent, useState, useTransition } from 'react';
import { Button, Input, Select } from '@/components/ui';
import {
  DEFAULT_SORT,
  DIFFICULTY_OPTIONS,
  type RecipeListFilters,
  SORT_OPTIONS,
  toRecipeListQuery,
} from '@/lib/recipes';
import type { CategoryDto } from '@/lib/types';

interface Props {
  initial: RecipeListFilters;
  categories: CategoryDto[];
}

/**
 * FR-SRCH-002/003 — bộ lọc + sắp xếp. Áp dụng bằng cách đổi URL để trang SSR render lại;
 * luôn về trang 1 khi đổi bộ lọc (FR-SRCH-004).
 */
export default function RecipeFilters({ initial, categories }: Props) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [filters, setFilters] = useState<RecipeListFilters>({
    categoryId: initial.categoryId ?? '',
    difficulty: initial.difficulty ?? '',
    maxCookTime: initial.maxCookTime ?? '',
    minServings: initial.minServings ?? '',
    sort: initial.sort || DEFAULT_SORT,
  });

  const set = (key: keyof RecipeListFilters) => (value: string) =>
    setFilters((prev) => ({ ...prev, [key]: value }));

  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const query = toRecipeListQuery({ ...filters, page: 1 }).toString();
    startTransition(() => router.push(query ? `/recipes?${query}` : '/recipes'));
  };

  const hasFilters = Boolean(
    initial.categoryId ||
      initial.difficulty ||
      initial.maxCookTime ||
      initial.minServings ||
      (initial.sort && initial.sort !== DEFAULT_SORT),
  );

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Bộ lọc công thức"
      className="grid grid-cols-1 gap-4 rounded-lg border border-border bg-surface p-4 sm:grid-cols-2 lg:grid-cols-5"
    >
      <Select
        label="Danh mục"
        value={filters.categoryId}
        onChange={(e) => set('categoryId')(e.target.value)}
        options={[{ value: '', label: 'Tất cả' }, ...categories.map((c) => ({ value: c.id, label: c.name }))]}
      />
      <Select
        label="Độ khó"
        value={filters.difficulty}
        onChange={(e) => set('difficulty')(e.target.value)}
        options={[{ value: '', label: 'Tất cả' }, ...DIFFICULTY_OPTIONS]}
      />
      <Input
        label="Nấu tối đa (phút)"
        type="number"
        inputMode="numeric"
        min={0}
        value={filters.maxCookTime}
        onChange={(e) => set('maxCookTime')(e.target.value)}
      />
      <Input
        label="Khẩu phần tối thiểu"
        type="number"
        inputMode="numeric"
        min={1}
        value={filters.minServings}
        onChange={(e) => set('minServings')(e.target.value)}
      />
      <Select
        label="Sắp xếp"
        value={filters.sort}
        onChange={(e) => set('sort')(e.target.value)}
        options={[...SORT_OPTIONS]}
      />

      <div className="flex items-center justify-end gap-3 sm:col-span-2 lg:col-span-5">
        {hasFilters && (
          <Link href="/recipes" className="text-sm font-medium text-brand-700 hover:underline">
            Xóa bộ lọc
          </Link>
        )}
        <Button type="submit" disabled={isPending}>
          {isPending ? 'Đang lọc…' : 'Áp dụng'}
        </Button>
      </div>
    </form>
  );
}
