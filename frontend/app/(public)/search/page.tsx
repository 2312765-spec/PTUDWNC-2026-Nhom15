import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Skeleton } from '@/components/ui';
import { SEARCH_MIN_LENGTH } from '@/lib/recipes';
import SearchForm from './SearchForm';
import SearchResults from './SearchResults';

/** FR-SRCH-001 — SRS 5.1: `/search` SSR (mỗi request render lại theo `?q=`). */
export const dynamic = 'force-dynamic';

interface PageProps {
  searchParams: Promise<{ q?: string | string[]; page?: string | string[] }>;
}

function first(value: string | string[] | undefined): string {
  return (Array.isArray(value) ? value[0] : value) ?? '';
}

async function readParams(searchParams: PageProps['searchParams']) {
  const params = await searchParams;
  const q = first(params.q).trim();
  const page = Math.max(1, Number.parseInt(first(params.page), 10) || 1);
  return { q, page };
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { q } = await readParams(searchParams);
  return { title: q ? `Tìm kiếm: ${q}` : 'Tìm kiếm công thức' };
}

export default async function SearchPage({ searchParams }: PageProps) {
  const { q, page } = await readParams(searchParams);

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-8">
      <header className="space-y-2 border-b border-border pb-4">
        <h1 className="text-3xl font-bold">Tìm kiếm công thức</h1>
        <p className="text-sm text-ink-muted">
          Gõ có dấu hoặc không dấu đều được — ví dụ &quot;pho bo&quot; vẫn ra &quot;Phở bò&quot;.
        </p>
        <SearchForm key={q} initialQuery={q} />
      </header>

      {q.length >= SEARCH_MIN_LENGTH && (
        <Suspense key={`${q}|${page}`} fallback={<ResultsSkeleton />}>
          <SearchResults q={q} page={page} />
        </Suspense>
      )}
    </div>
  );
}

function ResultsSkeleton() {
  return (
    <div aria-busy="true" className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      {Array.from({ length: 4 }, (_, i) => (
        <Skeleton key={i} className="h-56" />
      ))}
    </div>
  );
}
