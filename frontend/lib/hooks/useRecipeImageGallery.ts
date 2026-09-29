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
  const [images, setImagesState] = useState<RecipeImageDto[]>([...initialImages].sort(byOrderIndex));
  // Nguồn sự thật ĐỒNG BỘ. Không dựa vào updater của setState: React chạy updater lười (nhất là khi
  // đã có update khác chờ trong cùng batch), nên kết quả tính trong updater KHÔNG dùng được ngay sau
  // khi gọi setState — reorder cần biết ảnh nào đổi orderIndex để PATCH.
  const imagesRef = useRef(images);
  const setImages = useCallback((update: (current: RecipeImageDto[]) => RecipeImageDto[]) => {
    imagesRef.current = update(imagesRef.current);
    setImagesState(imagesRef.current);
  }, []);
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

        // Backend chỉ trả 4 trường (D27), không có orderIndex — server gán Max(hiện có)+1 (ảnh đầu = 0),
        // gallery tính lại cùng công thức để sort/reorder không gặp `undefined`. mediumUrl/thumbnailUrl
        // = null cho tới khi FR-JOB-002 resize xong.
        setImages((current) => {
          const orderIndex = current.length === 0 ? 0 : Math.max(...current.map((img) => img.orderIndex)) + 1;
          return [...current, { ...uploaded, mediumUrl: null, thumbnailUrl: null, orderIndex }].sort(byOrderIndex);
        });
      } finally {
        setUploadingFiles((current) => current.filter((item) => item.id !== placeholderId));
      }
    },
    [uploadImage, setImages],
  );

  const setPrimary = useCallback(
    async (imageId: string) => {
      await updateImage(imageId, { isPrimary: true });
      setImages((current) => current.map((img) => ({ ...img, isPrimary: img.imageId === imageId })));
    },
    [updateImage, setImages],
  );

  const updateAltText = useCallback(
    async (imageId: string, altText: string) => {
      await updateImage(imageId, { altText });
      setImages((current) => current.map((img) => (img.imageId === imageId ? { ...img, altText } : img)));
    },
    [updateImage, setImages],
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
    [deleteImage, setImages],
  );

  const reorder = useCallback(
    async (draggedImageId: string, targetImageId: string) => {
      if (draggedImageId === targetImageId) {
        return;
      }

      const previous = imagesRef.current;
      const fromIndex = previous.findIndex((img) => img.imageId === draggedImageId);
      const toIndex = previous.findIndex((img) => img.imageId === targetImageId);
      if (fromIndex === -1 || toIndex === -1) {
        return;
      }

      const reordered = [...previous];
      const [moved] = reordered.splice(fromIndex, 1);
      reordered.splice(toIndex, 0, moved);

      // Chỉ PATCH đúng những ảnh thật sự đổi orderIndex (D27: cho phép trùng, không renumber toàn bộ).
      const changed: { imageId: string; orderIndex: number }[] = [];
      const next = reordered.map((img, index) => {
        if (img.orderIndex !== index) {
          changed.push({ imageId: img.imageId, orderIndex: index });
        }
        return { ...img, orderIndex: index };
      });

      // Optimistic: UI đổi ngay, không đợi server.
      setImages(() => next);

      const results = await Promise.allSettled(
        changed.map(({ imageId, orderIndex }) => updateImage(imageId, { orderIndex })),
      );

      // Lỗi đã hiện toast ở useRecipeImages. Trả UI về thứ tự cũ để không hiển thị một thứ tự mà server
      // không có. Nếu chỉ một phần PATCH thành công thì server có thể lệch tạm tới lần tải lại kế tiếp.
      if (results.some((result) => result.status === 'rejected')) {
        // Chỉ khôi phục orderIndex (không ghi đè cả mảng): trong lúc chờ có thể đã có ảnh mới upload
        // xong hoặc alt text vừa sửa — những thay đổi đó phải được giữ.
        const previousOrder = new Map(previous.map((img) => [img.imageId, img.orderIndex]));
        setImages((current) =>
          current
            .map((img) => ({ ...img, orderIndex: previousOrder.get(img.imageId) ?? img.orderIndex }))
            .sort(byOrderIndex),
        );
      }
    },
    [updateImage, setImages],
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
