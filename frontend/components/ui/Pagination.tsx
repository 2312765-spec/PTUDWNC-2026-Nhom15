import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface PaginationProps {
  /** 1-based, khớp PagedResult<T>.page ở backend. */
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  className?: string;
}

const ELLIPSIS = '…' as const;

function buildPageList(page: number, totalPages: number): (number | typeof ELLIPSIS)[] {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  }

  const pages = new Set<number>([1, totalPages, page, page - 1, page + 1]);
  const sorted = [...pages].filter((p) => p >= 1 && p <= totalPages).sort((a, b) => a - b);

  const result: (number | typeof ELLIPSIS)[] = [];
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) {
      result.push(ELLIPSIS);
    }
    result.push(p);
  });

  return result;
}

export function Pagination({ page, totalPages, onPageChange, className }: PaginationProps) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav aria-label="Phân trang" className={cn('flex items-center justify-center gap-1', className)}>
      <button
        type="button"
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
        aria-label="Trang trước"
        className="inline-flex size-9 items-center justify-center rounded-md text-ink hover:bg-surface-subtle disabled:opacity-40"
      >
        <ChevronLeft className="size-4" aria-hidden="true" />
      </button>

      {buildPageList(page, totalPages).map((p, i) =>
        p === ELLIPSIS ? (
          <span key={`ellipsis-${i}`} className="px-1 text-ink-muted" aria-hidden="true">
            {ELLIPSIS}
          </span>
        ) : (
          <button
            key={p}
            type="button"
            onClick={() => onPageChange(p)}
            aria-current={p === page ? 'page' : undefined}
            className={cn(
              'inline-flex size-9 items-center justify-center rounded-md text-sm',
              p === page ? 'bg-brand-600 text-white' : 'text-ink hover:bg-surface-subtle',
            )}
          >
            {p}
          </button>
        ),
      )}

      <button
        type="button"
        onClick={() => onPageChange(page + 1)}
        disabled={page >= totalPages}
        aria-label="Trang sau"
        className="inline-flex size-9 items-center justify-center rounded-md text-ink hover:bg-surface-subtle disabled:opacity-40"
      >
        <ChevronRight className="size-4" aria-hidden="true" />
      </button>
    </nav>
  );
}
