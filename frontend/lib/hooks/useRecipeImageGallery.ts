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

  /**
   * NFR-USE-004 — optimistic: đổi primary ngay rồi mới PATCH. Lỗi → trả cờ primary cũ (toast lỗi ở
   * useRecipeImages) và ném lỗi ra. Chỉ hoàn tác khi ảnh này VẪN đang là primary: nếu trong lúc chờ
   * người dùng đã đặt ảnh khác làm primary (hoặc xoá ảnh này) thì giữ trạng thái mới hơn đó.
   */
  const setPrimary = useCallback(
    async (imageId: string) => {
      const previousPrimaryIds = new Set(
        imagesRef.current.filter((img) => img.isPrimary).map((img) => img.imageId),
      );
      setImages((current) => current.map((img) => ({ ...img, isPrimary: img.imageId === imageId })));

      try {
        await updateImage(imageId, { isPrimary: true });
      } catch (error) {
        setImages((current) => {
          const stillOurs = current.some((img) => img.imageId === imageId && img.isPrimary);
          const previousStillThere = current.some((img) => previousPrimaryIds.has(img.imageId));
          if (!stillOurs || !previousStillThere) {
            return current;
          }
          return current.map((img) => ({ ...img, isPrimary: previousPrimaryIds.has(img.imageId) }));
        });
        throw error;
      }
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

  /**
   * NFR-USE-004 — optimistic: gỡ ảnh + chuyển primary sang ảnh orderIndex nhỏ nhất (D22) ngay, rồi mới
   * DELETE. Lỗi → chèn ảnh lại theo orderIndex, trả primary về nó nếu ảnh được đôn lên vẫn đang là
   * primary, rồi ném lỗi ra. Không ghi đè cả mảng: ảnh upload xong / alt text sửa trong lúc chờ được giữ.
   */
  const remove = useCallback(
    async (imageId: string) => {
      const removed = imagesRef.current.find((img) => img.imageId === imageId);
      let promotedId: string | null = null;

      setImages((current) => {
        const rest = current.filter((img) => img.imageId !== imageId);

        if (!removed?.isPrimary || rest.length === 0) {
          return rest;
        }

        const [next, ...others] = rest.sort(byOrderIndex);
        promotedId = next.imageId;
        return [{ ...next, isPrimary: true }, ...others];
      });

      try {
        await deleteImage(imageId);
      } catch (error) {
        if (removed) {
          setImages((current) => {
            if (current.some((img) => img.imageId === imageId)) {
              return current;
            }
            const promotedStillPrimary =
              promotedId === null || current.some((img) => img.imageId === promotedId && img.isPrimary);
            const restorePrimary = removed.isPrimary && promotedStillPrimary;
            const others = restorePrimary ? current.map((img) => ({ ...img, isPrimary: false })) : current;
            return [...others, { ...removed, isPrimary: restorePrimary }].sort(byOrderIndex);
          });
        }
        throw error;
      }
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
