'use client';

import { UploadCloud } from 'lucide-react';
import { useId, useRef, useState } from 'react';
import { cn } from '@/lib/utils';
import { ACCEPTED_IMAGE_MIME_TYPES, validateImageFile } from '@/lib/validation/imageFile';

export interface ImageUploadDropzoneProps {
  onFilesSelected: (files: File[]) => void;
  disabled?: boolean;
}

/**
 * FR-RCP-008 (FE) — pre-check CONS-007 ở client trước khi gọi API upload thật
 * (useRecipeImageGallery.upload). Chỉ để phản hồi nhanh, không thay được validate
 * server (D28 kiểm magic bytes).
 */
export function ImageUploadDropzone({ onFilesSelected, disabled = false }: ImageUploadDropzoneProps) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  function acceptFiles(files: FileList | File[]) {
    const list = Array.from(files);
    if (list.length === 0) {
      return;
    }

    for (const file of list) {
      const result = validateImageFile(file);
      if (!result.ok) {
        setError(`${file.name} — ${result.message}`);
        return;
      }
    }

    setError(null);
    onFilesSelected(list);
  }

  return (
    <div
      data-testid="image-dropzone"
      onDragOver={(e) => {
        e.preventDefault();
        if (!disabled) {
          setIsDragging(true);
        }
      }}
      onDragLeave={() => setIsDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setIsDragging(false);
        if (disabled) {
          return;
        }
        if (e.dataTransfer?.files) {
          acceptFiles(e.dataTransfer.files);
        }
      }}
      className={cn(
        'flex flex-col items-center gap-3 rounded-xl border-2 border-dashed p-10 text-center transition-colors',
        isDragging ? 'border-brand-600 bg-brand-50' : 'border-border bg-surface',
      )}
    >
      <div className="flex size-14 items-center justify-center rounded-full bg-brand-100">
        <UploadCloud className="size-6 text-brand-600" aria-hidden="true" />
      </div>
      <div className="text-sm font-semibold text-ink">Kéo thả ảnh vào đây</div>
      <div className="text-xs text-ink-muted">hoặc</div>
      <button
        type="button"
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
        className="h-10 rounded-md bg-brand-600 px-5 text-sm font-semibold text-white transition-colors hover:bg-brand-700 disabled:cursor-not-allowed disabled:bg-brand-600/50"
      >
        Chọn ảnh từ máy
      </button>
      <label htmlFor={inputId} className="sr-only">
        Chọn ảnh để tải lên
      </label>
      <input
        id={inputId}
        ref={inputRef}
        type="file"
        accept={ACCEPTED_IMAGE_MIME_TYPES.join(',')}
        multiple
        disabled={disabled}
        className="sr-only"
        onChange={(e) => {
          if (e.target.files) {
            acceptFiles(e.target.files);
          }
          // Reset để chọn lại đúng file vừa bị từ chối (input giữ giá trị cũ thì onChange không bắn lại).
          e.target.value = '';
        }}
      />
      {error && (
        <p role="alert" className="text-xs font-medium text-red-700">
          {error}
        </p>
      )}
    </div>
  );
}
