'use client';

import Link from 'next/link';
import { useEffect } from 'react';
import { Button } from '@/components/ui';

/**
 * FR-RCP-002 / NFR-USE-003 — backend lỗi khi tải chi tiết công thức. Khác 404: công thức vẫn có thể
 * tồn tại, chỉ là tải hỏng. Khi ISR revalidate gặp lỗi, Next giữ bản cũ đã cache — trang này chỉ hiện
 * khi chưa có bản nào.
 */
export default function RecipeDetailError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div role="alert" className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
      <h1 className="text-xl font-bold text-ink">Không tải được công thức</h1>
      <p className="mt-2 text-sm text-ink-muted">Máy chủ đang gặp sự cố. Vui lòng thử lại sau ít phút.</p>
      <div className="mt-6 flex gap-3">
        <Button type="button" onClick={reset}>
          Thử lại
        </Button>
        <Link
          href="/recipes"
          className="inline-flex h-10 items-center rounded-md border border-border bg-surface px-4 text-sm font-medium text-ink hover:bg-surface-subtle"
        >
          Về danh sách công thức
        </Link>
      </div>
    </div>
  );
}
