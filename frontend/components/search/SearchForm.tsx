'use client';

import { useRouter } from 'next/navigation';
import { type FormEvent, useState, useTransition } from 'react';
import { Button, Input } from '@/components/ui';
import { SEARCH_MIN_LENGTH } from '@/lib/recipes';

/**
 * FR-SRCH-001 — chỉ phần nhập liệu là client. Submit đổi URL `?q=` để trang SSR tải kết quả,
 * nhờ vậy link kết quả chia sẻ được và nút Back hoạt động. Dùng chung cho `/search` và trang chủ.
 *
 * `action="/search"` + `method="get"`: khi JS chưa tải xong, trình duyệt tự gửi `?q=` tới `/search`
 * (trang đó tự trim và bỏ qua q < 2 ký tự). Khi đã có JS, onSubmit chặn lại để validate tiếng Việt
 * và chuyển trang mềm bằng router.
 */
export default function SearchForm({ initialQuery, stacked = false }: { initialQuery: string; stacked?: boolean }) {
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
    <form
      role="search"
      action="/search"
      method="get"
      onSubmit={onSubmit}
      // D97: `stacked` — ô nhập và nút xếp dọc cho thanh bên hẹp.
      className={stacked ? 'flex flex-col gap-2' : 'flex items-start gap-2'}
      noValidate
    >
      <div className={stacked ? undefined : 'flex-1'}>
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
