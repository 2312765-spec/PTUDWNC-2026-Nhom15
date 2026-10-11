'use client';

import { useRouter } from 'next/navigation';
import { Pagination } from '@/components/ui';

/** FR-SRCH-004 — đổi trang bằng URL `?q=&page=` để trang SSR render lại. */
export default function SearchPagination({
  q,
  page,
  totalPages,
}: {
  q: string;
  page: number;
  totalPages: number;
}) {
  const router = useRouter();

  return (
    <Pagination
      page={page}
      totalPages={totalPages}
      onPageChange={(p) => router.push(`/search?q=${encodeURIComponent(q)}&page=${p}`)}
    />
  );
}
