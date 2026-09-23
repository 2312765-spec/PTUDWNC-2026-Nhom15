import { useCallback, useRef, useState } from 'react';
import { useRecipeImages } from '@/lib/hooks/useRecipeImages';
import type { RecipeImageDto } from '@/lib/types';

export interface UploadingFile {
  id: string;
  fileName: string;
  progress: number;
}

function byOrderIndex(a: RecipeImageDto, b: RecipeImageDto): number {
  return a.orderIndex - b.orderIndex;
}

/**
 * Bọc useRecipeImages để giữ mảng ảnh + trạng thái "đang tải" cục bộ cho
 * RecipeImageManager — không phụ thuộc GET recipe detail (FR-RCP-002 chưa xong).
 * `initialImages` truyền vào khi C nối xong endpoint đó; rỗng cho luồng tạo mới.
 *
 * Logic "chỉ 1 ảnh primary" và "xoá primary → ảnh orderIndex nhỏ nhất lên thay" (D22)
 * được lặp lại Ở ĐÂY chỉ để UI phản hồi ngay, không cần đợi reload — server (Domain
 * Recipe) vẫn là nguồn sự thật, nếu lệch thì lần load lại sau ghi đè state này.
 */
export function useRecipeImageGallery(recipeId: string, initialImages: RecipeImageDto[] = []) {
  const [images, setImages] = useState<RecipeImageDto[]>([...initialImages].sort(byOrderIndex));
  const [uploadingFiles, setUploadingFiles] = useState<UploadingFile[]>([]);
  const placeholderSeq = useRef(0);

  const { uploadImage, updateImage, deleteImage, isUploading, isUpdating, isDeleting } =
    useRecipeImages(recipeId);

  const upload = useCallback(
    async (file: File, altText?: string) => {
      const placeholderId = `upload-${placeholderSeq.current++}`;
      setUploadingFiles((current) => [...current, { id: placeholderId, fileName: file.name, progress: 0 }]);

      try {
        const uploaded = await uploadImage(file, altText, (percent) => {
          setUploadingFiles((current) =>
            current.map((item) => (item.id === placeholderId ? { ...item, progress: percent } : item)),
          );
        });

        setImages((current) => [...current, uploaded].sort(byOrderIndex));
      } finally {
        setUploadingFiles((current) => current.filter((item) => item.id !== placeholderId));
      }
    },
    [uploadImage],
  );

  const setPrimary = useCallback(
    async (imageId: string) => {
      await updateImage(imageId, { isPrimary: true });
      setImages((current) => current.map((img) => ({ ...img, isPrimary: img.imageId === imageId })));
    },
    [updateImage],
  );

  const updateAltText = useCallback(
    async (imageId: string, altText: string) => {
      await updateImage(imageId, { altText });
      setImages((current) => current.map((img) => (img.imageId === imageId ? { ...img, altText } : img)));
    },
    [updateImage],
  );

  const remove = useCallback(
    async (imageId: string) => {
      await deleteImage(imageId);

      setImages((current) => {
        const removed = current.find((img) => img.imageId === imageId);
        const rest = current.filter((img) => img.imageId !== imageId);

        if (!removed?.isPrimary || rest.length === 0) {
          return rest;
        }

        const [next, ...others] = rest.sort(byOrderIndex);
        return [{ ...next, isPrimary: true }, ...others];
      });
    },
    [deleteImage],
  );

  const reorder = useCallback(
    async (draggedImageId: string, targetImageId: string) => {
      if (draggedImageId === targetImageId) {
        return;
      }

      // Đọc kết quả từ trong updater để tránh stale closure trên `images`, đồng thời lấy
      // ra danh sách thật sự đổi orderIndex — chỉ PATCH đúng những ảnh đó (D27: cho phép
      // trùng orderIndex, không cần renumber toàn bộ).
      let changed: { imageId: string; orderIndex: number }[] = [];

      setImages((current) => {
        const fromIndex = current.findIndex((img) => img.imageId === draggedImageId);
        const toIndex = current.findIndex((img) => img.imageId === targetImageId);
        if (fromIndex === -1 || toIndex === -1) {
          return current;
        }

        const reordered = [...current];
        const [moved] = reordered.splice(fromIndex, 1);
        reordered.splice(toIndex, 0, moved);

        changed = [];
        return reordered.map((img, index) => {
          if (img.orderIndex !== index) {
            changed.push({ imageId: img.imageId, orderIndex: index });
          }
          return { ...img, orderIndex: index };
        });
      });

      await Promise.all(
        changed.map(({ imageId, orderIndex }) => updateImage(imageId, { orderIndex }).catch(() => {})),
      );
    },
    [updateImage],
  );

  return {
    images,
    uploadingFiles,
    upload,
    setPrimary,
    updateAltText,
    remove,
    reorder,
    isUploading,
    isUpdating,
    isDeleting,
  };
}
