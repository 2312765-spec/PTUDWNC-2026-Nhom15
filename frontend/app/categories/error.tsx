'use client';

import { Button } from '@/components/ui';

/** Hiện khi API lỗi lần đầu (chưa có bản ISR cũ để giữ lại). */
export default function CategoriesError({ reset }: { error: Error; reset: () => void }) {
  return (
    <div role="alert" className="mx-auto max-w-xl px-4 py-16 text-center">
      <h1 className="text-xl font-semibold">Không tải được danh mục</h1>
      <p className="mt-2 text-sm text-ink-muted">Đã có lỗi khi kết nối máy chủ. Vui lòng thử lại.</p>
      <Button className="mt-6" onClick={reset}>
        Thử lại
      </Button>
    </div>
  );
}
