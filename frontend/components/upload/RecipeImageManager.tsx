'use client';

import { useRef, useState } from 'react';
import { ImageDeleteDialog } from '@/components/upload/ImageDeleteDialog';
import { ImageUploadDropzone } from '@/components/upload/ImageUploadDropzone';
import { RecipeImageCard } from '@/components/upload/RecipeImageCard';
import { useRecipeImageGallery } from '@/lib/hooks/useRecipeImageGallery';
import type { RecipeImageDto } from '@/lib/types';

export interface RecipeImageManagerProps {
  recipeId: string;
  /** Truyền từ GET recipe detail khi C nối xong FR-RCP-002; rỗng cho luồng tạo mới. */
  initialImages?: RecipeImageDto[];
}

/**
 * FR-RCP-008 (FE) — bước "Ảnh minh hoạ" trong wizard tạo/sửa công thức (nhúng vào
 * dashboard/recipes/[id]/edit của C). Ghép ImageUploadDropzone + RecipeImageCard +
 * ImageDeleteDialog trên nền useRecipeImageGallery.
 */
export function RecipeImageManager({ recipeId, initialImages = [] }: RecipeImageManagerProps) {
  const gallery = useRecipeImageGallery(recipeId, initialImages);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);
  const draggedImageId = useRef<string | null>(null);
  const uploadQueue = useRef<Promise<void>>(Promise.resolve());

  function handleFilesSelected(files: File[]) {
    // Hàng đợi TUẦN TỰ dùng chung cho mọi lần chọn/thả file, không Promise.all: recipe chưa có ảnh thì
    // mọi upload đồng thời cùng thấy "chưa có ảnh" và cùng đòi làm ảnh primary (D22/D27). Xếp vào cùng
    // một chuỗi (không phải mỗi lần chọn một chuỗi) để thả thêm file lúc đợt trước còn chạy cũng
    // không chạy song song.
    for (const file of files) {
      uploadQueue.current = uploadQueue.current
        .then(() => gallery.upload(file))
        // Lỗi đã hiện toast ở useRecipeImages — bỏ qua file lỗi, vẫn tải tiếp các file còn lại.
        .catch(() => {});
    }
  }

  async function confirmDelete() {
    if (!pendingDeleteId) {
      return;
    }
    try {
      await gallery.remove(pendingDeleteId);
      setPendingDeleteId(null);
    } catch {
      // Lỗi đã hiện toast ở useRecipeImages — giữ dialog mở để thử lại hoặc Huỷ.
    }
  }

  const hasTiles = gallery.images.length > 0 || gallery.uploadingFiles.length > 0;

  return (
    <div className="flex flex-col gap-4">
      <div className="text-sm font-semibold text-ink">Ảnh của bạn ({gallery.images.length})</div>

      {hasTiles && (
        <div className="grid grid-cols-3 gap-5">
          {gallery.images.map((img) => (
            <RecipeImageCard
              key={img.imageId}
              image={img}
              onSetPrimary={gallery.setPrimary}
              onUpdateAltText={gallery.updateAltText}
              onDelete={setPendingDeleteId}
              onDragStart={() => {
                draggedImageId.current = img.imageId;
              }}
              onDrop={() => {
                const draggedId = draggedImageId.current;
                draggedImageId.current = null;
                if (draggedId && draggedId !== img.imageId) {
                  gallery.reorder(draggedId, img.imageId).catch(() => {});
                }
              }}
            />
          ))}

          {gallery.uploadingFiles.map((file) => (
            <div key={file.id} className="flex flex-col gap-1.5">
              <div className="relative h-[140px] w-full overflow-hidden rounded-lg border border-border bg-surface-subtle">
                <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-ink/75 to-transparent p-2">
                  <div className="h-1.5 overflow-hidden rounded-full bg-white/35">
                    <div className="h-full bg-brand-500" style={{ width: `${file.progress}%` }} />
                  </div>
                  <div className="mt-1 text-[10.5px] font-semibold text-white">Đang tải — {file.progress}%</div>
                </div>
              </div>
              <div className="truncate text-xs text-ink-muted">{file.fileName}</div>
            </div>
          ))}
        </div>
      )}

      <ImageUploadDropzone onFilesSelected={handleFilesSelected} disabled={gallery.isUploading} />

      <ImageDeleteDialog
        open={pendingDeleteId !== null}
        isDeleting={gallery.isDeleting}
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={confirmDelete}
      />
    </div>
  );
}
