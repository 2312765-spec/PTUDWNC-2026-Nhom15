'use client';

import { useRouter } from 'next/navigation';
import { type FormEvent, useState, useTransition } from 'react';
import { Button, Input } from '@/components/ui';
import { SEARCH_MIN_LENGTH } from '@/lib/recipes';

/**
 * FR-SRCH-001 — chỉ phần nhập liệu là client. Submit đổi URL `?q=` để trang SSR tải kết quả,
 * nhờ vậy link kết quả chia sẻ được và nút Back hoạt động.
 */
export default function SearchForm({ initialQuery }: { initialQuery: string }) {
  const router = useRouter();
  const [value, setValue] = useState(initialQuery);
  const [error, setError] = useState<string>();
  const [isPending, startTransition] = useTransition();

  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const q = value.trim();
    if (q.length < SEARCH_MIN_LENGTH) {
      setError(`Từ khóa tìm kiếm phải có ít nhất ${SEARCH_MIN_LENGTH} ký tự.`);
      return;
    }
    setError(undefined);
    startTransition(() => router.push(`/search?q=${encodeURIComponent(q)}`));
  };

  return (
    <form role="search" onSubmit={onSubmit} className="flex items-start gap-2" noValidate>
      <div className="flex-1">
        <Input
          type="search"
          name="q"
          aria-label="Từ khóa tìm kiếm"
          placeholder="Nhập tên món ăn… (tối thiểu 2 ký tự)"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          error={error}
        />
      </div>
      <Button type="submit" disabled={isPending}>
        {isPending ? 'Đang tìm…' : 'Tìm kiếm'}
      </Button>
    </form>
  );
}
