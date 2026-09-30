'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { CategoryFormDialog } from '@/components/categories/CategoryFormDialog';
import { DeleteCategoryButton } from '@/components/categories/DeleteCategoryButton';
import { Button, Skeleton } from '@/components/ui';
import { apiClient, toProblemDetails } from '@/lib/api-client';
import type { CategoryDto } from '@/lib/types';

const categoriesQueryKey = ['dashboard', 'categories'] as const;

async function fetchCategories(): Promise<CategoryDto[]> {
  const res = await apiClient.get<CategoryDto[]>('/categories');
  return res.data;
}

type FormState = { open: false } | { open: true; category: CategoryDto | null };

/**
 * Trang quản trị danh mục — Admin. SRS 5.1: /dashboard/* render CSR.
 * FR-CAT-001 (danh sách) · FR-CAT-003 (tạo) · FR-CAT-004 (sửa) · FR-CAT-005 (xóa mềm, D2).
 */
export default function AdminCategoriesPage() {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<FormState>({ open: false });

  const { data: categories, isPending, isError, error, refetch } = useQuery({
    queryKey: categoriesQueryKey,
    queryFn: fetchCategories,
  });

  const reload = () => queryClient.invalidateQueries({ queryKey: categoriesQueryKey });

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <header className="flex flex-col gap-4 border-b border-border pb-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-brand-700">Quản lý danh mục</h1>
          <p className="mt-1 text-sm text-ink-muted">Thêm, sửa, xóa danh mục món ăn.</p>
        </div>
        <Button type="button" onClick={() => setForm({ open: true, category: null })}>
          + Thêm danh mục
        </Button>
      </header>

      <section aria-label="Danh sách danh mục" className="mt-6 overflow-x-auto rounded-lg border border-border bg-surface">
        {isPending ? (
          <div aria-busy="true" aria-label="Đang tải danh sách danh mục" className="flex flex-col gap-3 p-4">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : isError ? (
          <div role="alert" className="flex flex-col items-center gap-3 p-8 text-center text-sm text-red-700">
            <p>{toProblemDetails(error).detail ?? 'Không tải được danh sách danh mục.'}</p>
            <Button type="button" variant="outline" size="sm" onClick={() => refetch()}>
              Thử lại
            </Button>
          </div>
        ) : categories.length === 0 ? (
          <p className="p-8 text-center text-sm text-ink-muted">Chưa có danh mục nào.</p>
        ) : (
          <table className="min-w-full divide-y divide-border text-left text-sm">
            <thead className="bg-surface-subtle text-ink-muted">
              <tr>
                <th scope="col" className="px-6 py-3 font-medium">Tên danh mục</th>
                <th scope="col" className="px-6 py-3 font-medium">Slug</th>
                <th scope="col" className="px-6 py-3 text-center font-medium">Số công thức</th>
                <th scope="col" className="px-6 py-3 text-right font-medium">
                  <span className="sr-only">Thao tác</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {categories.map((cat) => (
                <tr key={cat.id}>
                  <td className="px-6 py-4">
                    <p className="font-semibold text-ink">{cat.name}</p>
                    {cat.description && <p className="mt-0.5 line-clamp-1 text-xs text-ink-muted">{cat.description}</p>}
                  </td>
                  <td className="px-6 py-4 font-mono text-xs text-ink-muted">{cat.slug}</td>
                  <td className="px-6 py-4 text-center">{cat.recipeCount}</td>
                  <td className="px-6 py-4">
                    <div className="flex justify-end gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        aria-label={`Sửa danh mục ${cat.name}`}
                        onClick={() => setForm({ open: true, category: cat })}
                      >
                        Sửa
                      </Button>
                      <DeleteCategoryButton
                        categoryId={cat.id}
                        categoryName={cat.name}
                        recipeCount={cat.recipeCount}
                        onDeleted={reload}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <CategoryFormDialog
        open={form.open}
        category={form.open ? form.category : null}
        onClose={() => setForm({ open: false })}
        onSaved={reload}
      />
    </div>
  );
}
