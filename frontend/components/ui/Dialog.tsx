'use client';

import { X } from 'lucide-react';
import { type ReactNode, useEffect, useId, useRef } from 'react';
import { cn } from '@/lib/utils';

export interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children?: ReactNode;
  className?: string;
}

/**
 * Dùng thẻ <dialog> gốc — có sẵn focus trap, Esc để đóng, backdrop, và tự trả focus
 * về phần tử đã mở dialog khi đóng (NFR-USE-002 WCAG 2.1 AA).
 */
export function Dialog({ open, onClose, title, description, children, className }: DialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      onClick={(e) => {
        // Click ra ngoài nội dung (lên chính <dialog>) — coi như bấm backdrop.
        if (e.target === dialogRef.current) {
          onClose();
        }
      }}
      className={cn(
        'w-full max-w-md rounded-lg border border-border bg-surface p-0 shadow-xl',
        'backdrop:bg-ink/50',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-4 border-b border-border p-4">
        <div>
          <h2 id={titleId} className="text-base font-semibold text-ink">
            {title}
          </h2>
          {description && <p className="mt-1 text-sm text-ink-muted">{description}</p>}
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Đóng"
          className="shrink-0 rounded p-1 text-ink-muted hover:bg-surface-subtle"
        >
          <X className="size-4" aria-hidden="true" />
        </button>
      </div>
      <div className="p-4">{children}</div>
    </dialog>
  );
}
