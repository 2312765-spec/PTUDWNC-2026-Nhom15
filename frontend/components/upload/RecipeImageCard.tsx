'use client';

import { ChevronLeft, ChevronRight, GripVertical, Pencil, Star, Trash2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { cn } from '@/lib/utils';
import type { RecipeImageDto } from '@/lib/types';

export interface RecipeImageCardProps {
  image: RecipeImageDto;
  onSetPrimary: (imageId: string) => void;
  onUpdateAltText: (imageId: string, altText: string) => void;
  onDelete: (imageId: string) => void;
  /** Kéo-thả sắp xếp (D27 PATCH orderIndex) — có 2 prop này thì card mới kéo được. */
  onDragStart?: () => void;
  onDrop?: () => void;
  /**
   * Sắp xếp bằng nút — thay thế kéo-thả cho người dùng bàn phím/trình đọc màn hình/cảm ứng
   * (WCAG 2.1.1). Không truyền = ảnh đang ở đầu/cuối, không có nút tương ứng.
   */
  onMoveEarlier?: () => void;
  onMoveLater?: () => void;
  /**
   * Sau khi di chuyển, card được React đặt lại ở vị trí mới trong DOM nên mất focus. Manager truyền
   * nút vừa bấm để card tự lấy lại focus (nếu nút đó không còn — ảnh vừa tới đầu/cuối — thì focus
   * sang nút còn lại), rồi báo `onFocusHandled`.
   */
  focusAction?: 'earlier' | 'later';
  onFocusHandled?: () => void;
}

/**
 * 1 ảnh trong gallery quản lý — dashboard/CSR nên dùng <img> thường, không next/image
 * (rule next/image ở CLAUDE.md áp cho trang công khai/SEO, không áp cho tool quản trị này).
 */
export function RecipeImageCard({
  image,
  onSetPrimary,
  onUpdateAltText,
  onDelete,
  onDragStart,
  onDrop,
  onMoveEarlier,
  onMoveLater,
  focusAction,
  onFocusHandled,
}: RecipeImageCardProps) {
  const [isEditingAlt, setIsEditingAlt] = useState(false);
  const [draftAltText, setDraftAltText] = useState(image.altText ?? '');
  // Enter (keydown) đóng ô input trước khi blur (mất focus do unmount) kịp bắn — cờ này
  // chặn commitAltText chạy 2 lần cho cùng 1 lần sửa.
  const skipNextBlurCommit = useRef(false);
  const moveEarlierRef = useRef<HTMLButtonElement>(null);
  const moveLaterRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!focusAction) {
      return;
    }
    const preferred = focusAction === 'earlier' ? moveEarlierRef.current : moveLaterRef.current;
    (preferred ?? moveEarlierRef.current ?? moveLaterRef.current)?.focus();
    onFocusHandled?.();
  }, [focusAction, onFocusHandled]);

  // FR-JOB-002 chạy nền, không SignalR — mediumUrl/thumbnailUrl null ngay sau upload.
  const displaySrc = image.thumbnailUrl ?? image.mediumUrl ?? image.originalUrl;
  // Tắt draggable khi đang sửa alt-text để không xung đột với chọn text trong input.
  const isDraggable = Boolean(onDragStart) && !isEditingAlt;

  function startEditing() {
    setDraftAltText(image.altText ?? '');
    setIsEditingAlt(true);
  }

  function commitAltText(value: string) {
    setIsEditingAlt(false);
    if (value !== (image.altText ?? '')) {
      onUpdateAltText(image.imageId, value);
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div
        data-testid="recipe-image-card"
        draggable={isDraggable}
        onDragStart={() => onDragStart?.()}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          onDrop?.();
        }}
        className={cn(
          'group relative overflow-hidden rounded-lg border box-border',
          image.isPrimary ? 'border-[3px] border-brand-600' : 'border border-border',
        )}
      >
        {/* eslint-disable-next-line @next/next/no-img-element -- thumbnail quản trị, không phải ảnh trang công khai (không áp NFR-SEO/next-image) */}
        <img src={displaySrc} alt={image.altText ?? ''} className="h-[140px] w-full object-cover" />

        {isDraggable && (
          <span className="absolute right-2 top-2 flex size-5 items-center justify-center rounded-full bg-white/70 text-ink-muted opacity-0 transition-opacity group-hover:opacity-100">
            <GripVertical className="size-3.5" aria-hidden="true" />
          </span>
        )}

        {image.isPrimary && (
          <span className="absolute left-2 top-2 inline-flex items-center gap-1 rounded-full bg-brand-600 px-2 py-0.5 text-[11px] font-bold text-white">
            <Star className="size-2.5 fill-white" aria-hidden="true" />
            Ảnh chính
          </span>
        )}

        <div className="absolute inset-x-0 bottom-0 flex justify-center gap-3 bg-ink/70 py-1.5 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
          {onMoveEarlier && (
            <button
              ref={moveEarlierRef}
              type="button"
              onClick={onMoveEarlier}
              aria-label="Đưa ảnh lên trước"
              className="text-white hover:text-brand-100"
            >
              <ChevronLeft className="size-4" aria-hidden="true" />
            </button>
          )}
          {!image.isPrimary && (
            <button
              type="button"
              onClick={() => onSetPrimary(image.imageId)}
              aria-label="Đặt làm ảnh chính"
              className="text-white hover:text-brand-100"
            >
              <Star className="size-4" aria-hidden="true" />
            </button>
          )}
          <button
            type="button"
            onClick={startEditing}
            aria-label="Sửa mô tả"
            className="text-white hover:text-brand-100"
          >
            <Pencil className="size-4" aria-hidden="true" />
          </button>
          <button
            type="button"
            onClick={() => onDelete(image.imageId)}
            aria-label="Xoá ảnh"
            className="text-white hover:text-red-300"
          >
            <Trash2 className="size-4" aria-hidden="true" />
          </button>
          {onMoveLater && (
            <button
              ref={moveLaterRef}
              type="button"
              onClick={onMoveLater}
              aria-label="Đưa ảnh ra sau"
              className="text-white hover:text-brand-100"
            >
              <ChevronRight className="size-4" aria-hidden="true" />
            </button>
          )}
        </div>
      </div>

      {isEditingAlt ? (
        <input
          autoFocus
          aria-label="Mô tả ảnh (alt text)"
          value={draftAltText}
          onChange={(e) => setDraftAltText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              skipNextBlurCommit.current = true;
              commitAltText(draftAltText);
            } else if (e.key === 'Escape') {
              skipNextBlurCommit.current = true;
              setIsEditingAlt(false);
            }
          }}
          onBlur={() => {
            if (skipNextBlurCommit.current) {
              skipNextBlurCommit.current = false;
              return;
            }
            commitAltText(draftAltText);
          }}
          className="h-7 w-full rounded border border-brand-600 px-1.5 text-xs outline-none"
        />
      ) : (
        <div className="truncate text-xs text-ink-muted">{image.altText || 'Chưa có mô tả'}</div>
      )}
    </div>
  );
}
