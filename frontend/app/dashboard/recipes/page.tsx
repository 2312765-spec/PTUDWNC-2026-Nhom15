'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { AlertTriangle, ExternalLink, Pencil, PlusCircle } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { Suspense, useRef, type KeyboardEvent } from 'react';
import { Badge, Button, Pagination, Skeleton } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import {
  fetchMyRecipes,
  RECIPE_ERROR_MESSAGES,
  recipeEditorKeys,
  type MyRecipesStatus,
} from '@/lib/recipe-editor-api';
import type { RecipeSummaryDto } from '@/lib/types';

interface Tab {
  /** Giá trị trên URL (`?status=`), `null` = Tất cả. */
  param: string | null;
  status: MyRecipesStatus | null;
  label: string;
}

const TABS: Tab[] = [
  { param: null, status: null, label: 'Tất cả' },
  { param: 'draft', status: 'Draft', label: 'Nháp' },
  { param: 'published', status: 'Published', label: 'Đã xuất bản' },
  { param: 'archived', status: 'Archived', label: 'Đã lưu trữ' },
];

const STATUS_BADGE: Record<string, { label: string; variant: 'default' | 'success' | 'warning' }> = {
  Draft: { label: 'Nháp', variant: 'warning' },
  Published: { label: 'Đã xuất bản', variant: 'success' },
  Archived: { label: 'Đã lưu trữ', variant: 'default' },
};

const dateFormat = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });

function ListSkeleton() {
  return (
    <div aria-busy="true" aria-label="Đang tải danh sách công thức" className="flex flex-col gap-3">
      {Array.from({ length: 4 }, (_, i) => (
        <Skeleton key={i} className="h-16 w-full" />
      ))}
    </div>
  );
}

function RecipeRow({ recipe }: { recipe: RecipeSummaryDto }) {
  const badge = STATUS_BADGE[recipe.status] ?? STATUS_BADGE.Draft;
  return (
    <li className="flex flex-wrap items-center gap-3 rounded-lg border border-border bg-surface p-4">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <Link
            href={`/dashboard/recipes/${recipe.id}/edit`}
            className="truncate font-semibold text-ink underline-offset-2 hover:underline"
          >
            {recipe.title}
          </Link>
          <Badge variant={badge.variant}>{badge.label}</Badge>
        </div>
        <p className="mt-1 text-sm text-ink-muted">
          Tạo ngày <time dateTime={recipe.createdAt}>{dateFormat.format(new Date(recipe.createdAt))}</time>
        </p>
      </div>
      <div className="flex gap-2">
        <Link
          href={`/dashboard/recipes/${recipe.id}/edit`}
          aria-label={`Sửa ${recipe.title}`}
          className="inline-flex h-9 items-center gap-1 rounded-md border border-border px-3 text-sm font-medium text-ink hover:bg-surface-subtle"
        >
          <Pencil className="size-4" aria-hidden="true" /> Sửa
        </Link>
        {recipe.status === 'Published' && (
          <Link
            href={`/recipes/${recipe.slug}`}
            aria-label={`Xem ${recipe.title}`}
            className="inline-flex h-9 items-center gap-1 rounded-md border border-border px-3 text-sm font-medium text-ink hover:bg-surface-subtle"
          >
            <ExternalLink className="size-4" aria-hidden="true" /> Xem
          </Link>
        )}
      </div>
    </li>
  );
}

function CreateLink() {
  return (
    <Link
      href="/dashboard/recipes/new"
      className="inline-flex h-10 items-center gap-2 rounded-md bg-brand-600 px-4 text-sm font-medium text-white hover:bg-brand-700"
    >
      <PlusCircle className="size-4" aria-hidden="true" /> Tạo công thức mới
    </Link>
  );
}

function MyRecipes() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);

  // URL là nguồn sự thật cho tab + trang (giữ được khi tải lại / bấm Back). Giá trị lạ → Tất cả / trang 1.
  const activeIndex = Math.max(0, TABS.findIndex((t) => t.param !== null && t.param === searchParams.get('status')));
  const active = TABS[activeIndex];
  const page = Math.max(1, Number.parseInt(searchParams.get('page') ?? '1', 10) || 1);

  const { data, isPending, isError, error, refetch, isFetching } = useQuery({
    queryKey: recipeEditorKeys.myRecipes(active.status, page),
    queryFn: () => fetchMyRecipes(active.status, page),
    placeholderData: keepPreviousData,
  });

  function navigate(tab: Tab, nextPage = 1) {
    const params = new URLSearchParams();
    if (tab.param) params.set('status', tab.param);
    if (nextPage > 1) params.set('page', String(nextPage));
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname);
  }

  // WAI-ARIA tabs: ←/→ chuyển tab (vòng tròn), Home/End về đầu/cuối.
  function onTabKeyDown(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    const last = TABS.length - 1;
    const next =
      event.key === 'ArrowRight' ? (index === last ? 0 : index + 1)
      : event.key === 'ArrowLeft' ? (index === 0 ? last : index - 1)
      : event.key === 'Home' ? 0
      : event.key === 'End' ? last
      : null;
    if (next === null) return;
    event.preventDefault();
    tabRefs.current[next]?.focus();
    navigate(TABS[next]);
  }

  let body;
  if (isPending) {
    body = <ListSkeleton />;
  } else if (isError) {
    const problem = toProblemDetails(error);
    body = (
      <div role="alert" className="flex flex-col items-center rounded-lg border border-border bg-surface p-8 text-center">
        <AlertTriangle className="size-8 text-red-600" aria-hidden="true" />
        <p className="mt-2 font-semibold text-ink">Không tải được danh sách công thức</p>
        <p className="mt-1 text-sm text-ink-muted">
          {RECIPE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? 'Vui lòng thử lại.'}
        </p>
        <Button type="button" variant="outline" className="mt-4" onClick={() => refetch()}>
          Thử lại
        </Button>
      </div>
    );
  } else if (data.items.length === 0) {
    body = (
      <div className="flex flex-col items-center rounded-lg border border-dashed border-border p-10 text-center">
        <p className="text-ink">
          {active.status ? `Không có công thức nào ở trạng thái ${active.label}.` : 'Bạn chưa có công thức nào.'}
        </p>
        {!active.status && (
          <div className="mt-4">
            <CreateLink />
          </div>
        )}
      </div>
    );
  } else {
    body = (
      <>
        <p className="mb-3 text-sm text-ink-muted" aria-live="polite">
          {data.totalCount} công thức
        </p>
        <ul aria-label="Công thức của tôi" aria-busy={isFetching} className="flex flex-col gap-3">
          {data.items.map((recipe) => (
            <RecipeRow key={recipe.id} recipe={recipe} />
          ))}
        </ul>
        <Pagination
          className="mt-6"
          page={data.page}
          totalPages={data.totalPages}
          onPageChange={(next) => navigate(active, next)}
        />
      </>
    );
  }

  return (
    <>
      <div role="tablist" aria-label="Lọc theo trạng thái" className="mb-6 flex flex-wrap gap-1 border-b border-border">
        {TABS.map((tab, index) => {
          const selected = index === activeIndex;
          return (
            <button
              key={tab.label}
              ref={(el) => {
                tabRefs.current[index] = el;
              }}
              type="button"
              role="tab"
              id={`my-recipes-tab-${index}`}
              aria-selected={selected}
              aria-controls="my-recipes-panel"
              tabIndex={selected ? 0 : -1}
              onClick={() => navigate(tab)}
              onKeyDown={(event) => onTabKeyDown(event, index)}
              className={
                selected
                  ? '-mb-px border-b-2 border-brand-600 px-4 py-2 text-sm font-semibold text-brand-700'
                  : '-mb-px border-b-2 border-transparent px-4 py-2 text-sm text-ink-muted hover:text-ink'
              }
            >
              {tab.label}
            </button>
          );
        })}
      </div>
      <div role="tabpanel" id="my-recipes-panel" aria-labelledby={`my-recipes-tab-${activeIndex}`}>
        {body}
      </div>
    </>
  );
}

/**
 * /dashboard/recipes — "công thức của tôi" (SRS 5.1: CSR, bắt buộc đăng nhập). FR-RCP-001 + D81: `GET /recipes?mine=true`
 * lọc theo `status`; Admin cũng chỉ thấy bài của mình. Quyền Author/Admin do layout.tsx chặn (chỉ UX — backend trả 401
 * nếu chưa đăng nhập). Tab + trang lưu trên URL (`?status=draft&page=2`).
 */
export default function MyRecipesPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <header className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold text-brand-700">Công thức của tôi</h1>
        <CreateLink />
      </header>
      {/* useSearchParams cần Suspense để Next build được trang. */}
      <Suspense fallback={<ListSkeleton />}>
        <MyRecipes />
      </Suspense>
    </div>
  );
}
