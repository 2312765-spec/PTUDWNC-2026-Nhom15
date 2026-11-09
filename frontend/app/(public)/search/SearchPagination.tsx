'use client';

import { useRouter } from 'next/navigation';
import { useTransition } from 'react';
import { Pagination } from '@/components/ui';

/**
 * FR-SRCH-004 — đổi trang bằng URL `?q=&page=` để trang SSR render lại.
 * NFR-USE-004/D86 — trang `/search` không còn skeleton của Suspense, nên chuyển trang trong transition để báo
 * đang tải (aria-busy, khóa nút, dòng trạng thái) trong khi kết quả cũ vẫn hiện, không bao giờ trống trang.
 */
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
  const [isPending, startTransition] = useTransition();

  return (
    <div className="space-y-2">
      <Pagination
        page={page}
        totalPages={totalPages}
        pending={isPending}
        onPageChange={(p) => startTransition(() => router.push(`/search?q=${encodeURIComponent(q)}&page=${p}`))}
      />
      {isPending && (
        <p role="status" className="text-center text-sm text-ink-muted">
          Đang tải trang…
        </p>
      )}
    </div>
  );
}
