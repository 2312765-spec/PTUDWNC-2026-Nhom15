import type { Metadata } from 'next';
import { SEARCH_MIN_LENGTH } from '@/lib/recipes';
import SearchForm from '@/components/search/SearchForm';
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
  return {
    title: q ? `Tìm kiếm: ${q}` : 'Tìm kiếm công thức',
    // D61.4: trang kết quả tìm kiếm nội bộ không lên Google, nhưng vẫn cho đi theo link tới công thức
    alternates: { canonical: '/search' },
    robots: { index: false, follow: true },
  };
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

      {/*
        NFR-PERF-005/D86: KHÔNG bọc <Suspense>. Bị stream thì kết quả nằm trong <div hidden> và chỉ hiện khi React
        chạy script ($RC/$RV, có hoãn) → ảnh card đầu (LCP) vẽ sau khi JS hydrate, LCP /search vượt 2.5s.
        API search nhanh (cache Redis) nên chờ kết quả rồi trả HTML một lần là đủ. Đang tải khi đổi từ khóa / trang:
        SearchForm ("Đang tìm…") và SearchPagination (aria-busy) báo — NFR-USE-004.
      */}
      {q.length >= SEARCH_MIN_LENGTH && <SearchResults q={q} page={page} />}
    </div>
  );
}
