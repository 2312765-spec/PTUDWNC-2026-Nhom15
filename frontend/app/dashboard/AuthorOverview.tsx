'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, PlusCircle } from 'lucide-react';
import Link from 'next/link';
import { Badge, Button, Skeleton } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import {
  fetchMyRecipesOverview,
  RECIPE_ERROR_MESSAGES,
  recipeEditorKeys,
  type MyRecipesOverview,
} from '@/lib/recipe-editor-api';
import { RECIPE_STATUS_BADGE } from '@/lib/recipe-status';

const HEADING_ID = 'author-overview-heading';

const dateFormat = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });

const STATS: { key: keyof MyRecipesOverview['counts']; label: string; href: string }[] = [
  { key: 'total', label: 'Tổng', href: '/dashboard/recipes' },
  { key: 'draft', label: 'Nháp', href: '/dashboard/recipes?status=draft' },
  { key: 'published', label: 'Đã xuất bản', href: '/dashboard/recipes?status=published' },
  { key: 'archived', label: 'Đã lưu trữ', href: '/dashboard/recipes?status=archived' },
];

function OverviewSkeleton() {
  return (
    <div aria-busy="true" aria-label="Đang tải tổng quan công thức">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {STATS.map(({ key }) => (
          <Skeleton key={key} className="h-20 w-full" />
        ))}
      </div>
      <Skeleton className="mt-6 h-5 w-40" />
      <div className="mt-3 flex flex-col gap-2">
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    </div>
  );
}

function Overview({ data }: { data: MyRecipesOverview }) {
  return (
    <>
      <ul aria-label="Số công thức theo trạng thái" className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {STATS.map(({ key, label, href }) => (
          <li key={key}>
            <Link
              href={href}
              className="flex h-full flex-col rounded-lg border border-border bg-surface p-4 transition-colors hover:border-brand-600 focus-visible:outline-2 focus-visible:outline-brand-600"
            >
              <span className="text-sm text-ink-muted">{label}</span>{' '}
              <span className="mt-1 text-2xl font-bold text-ink">{data.counts[key]}</span>
            </Link>
          </li>
        ))}
      </ul>

      {data.recent.length === 0 ? (
        <div className="mt-6 flex flex-col items-center rounded-lg border border-dashed border-border p-8 text-center">
          <p className="text-ink">Bạn chưa có công thức nào.</p>
          <Link
            href="/dashboard/recipes/new"
            className="mt-4 inline-flex h-10 items-center gap-2 rounded-md bg-brand-600 px-4 text-sm font-medium text-on-brand hover:bg-brand-700"
          >
            <PlusCircle className="size-4" aria-hidden="true" /> Tạo công thức đầu tiên
          </Link>
        </div>
      ) : (
        <div className="mt-6">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h3 className="font-semibold text-ink">Gần đây</h3>
            <Link
              href="/dashboard/recipes"
              aria-label="Xem tất cả công thức của tôi"
              className="inline-flex min-h-6 items-center text-sm font-medium text-brand-700 underline-offset-2 hover:underline"
            >
              Xem tất cả
            </Link>
          </div>
          <ul aria-label="Công thức gần đây" className="mt-3 flex flex-col gap-2">
            {data.recent.map((recipe) => {
              const badge = RECIPE_STATUS_BADGE[recipe.status] ?? RECIPE_STATUS_BADGE.Draft;
              return (
                <li
                  key={recipe.id}
                  className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-surface px-4 py-3"
                >
                  <Link
                    href={`/dashboard/recipes/${recipe.id}/edit`}
                    className="min-w-0 flex-1 truncate font-medium text-ink underline-offset-2 hover:underline"
                  >
                    {recipe.title}
                  </Link>
                  <Badge variant={badge.variant}>{badge.label}</Badge>
                  <time dateTime={recipe.createdAt} className="text-sm text-ink-muted">
                    {dateFormat.format(new Date(recipe.createdAt))}
                  </time>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </>
  );
}

/**
 * D82 — tổng quan "công thức của tôi" ở /dashboard (SRS 5.1): số bài theo trạng thái + 5 bài mới nhất. Chỉ trang cha
 * render cho Author/Admin. Lỗi chỉ hỏng phần này, lời chào và lối tắt vẫn dùng được.
 */
export default function AuthorOverview() {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: recipeEditorKeys.myRecipesOverview,
    queryFn: fetchMyRecipesOverview,
    // Trang tổng quan: luôn tải lại khi mở, kể cả vừa tạo bài ở wizard (wizard không invalidate danh sách).
    staleTime: 0,
  });

  let body;
  if (isPending) {
    body = <OverviewSkeleton />;
  } else if (isError) {
    const problem = toProblemDetails(error);
    body = (
      <div role="alert" className="flex flex-col items-center rounded-lg border border-border bg-surface p-6 text-center">
        <AlertTriangle className="size-8 text-danger" aria-hidden="true" />
        <p className="mt-2 font-semibold text-ink">Không tải được tổng quan công thức</p>
        <p className="mt-1 text-sm text-ink-muted">
          {RECIPE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? 'Vui lòng thử lại.'}
        </p>
        <Button type="button" variant="outline" className="mt-4" onClick={() => refetch()}>
          Thử lại
        </Button>
      </div>
    );
  } else {
    body = <Overview data={data} />;
  }

  return (
    <section aria-labelledby={HEADING_ID} className="mt-8">
      <h2 id={HEADING_ID} className="mb-4 text-lg font-semibold text-ink">
        Công thức của tôi
      </h2>
      {body}
    </section>
  );
}
