import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/Toast';
import { apiClient, toProblemDetails } from '@/lib/api-client';
import type { UploadRecipeImageResponse } from '@/lib/types';
import { IMAGE_MIME_ERROR_MESSAGE, IMAGE_SIZE_ERROR_MESSAGE } from '@/lib/validation/imageFile';

/**
 * D22/D27 — PATCH chấp nhận từng phần, field nào không gửi thì giữ nguyên giá trị cũ.
 */
export interface UpdateRecipeImagePatch {
  altText?: string;
  isPrimary?: boolean;
  orderIndex?: number;
}

/**
 * NFR-USE-003 — map theo error CODE (không theo message, message đổi được theo locale).
 * Bảng đối chiếu: docs/decisions.md § Danh sách Application Error Code.
 */
const IMAGE_ERROR_MESSAGES: Record<string, string> = {
  FILE_SIZE_EXCEEDED: IMAGE_SIZE_ERROR_MESSAGE,
  FILE_MIME_INVALID: IMAGE_MIME_ERROR_MESSAGE,
  RECIPE_NOT_FOUND: 'Không tìm thấy công thức.',
  RECIPE_IMAGE_NOT_FOUND: 'Không tìm thấy ảnh.',
  RECIPE_FORBIDDEN: 'Bạn không có quyền sửa ảnh của công thức này.',
  RECIPE_PRIMARY_IMAGE_REQUIRED: 'Công thức phải luôn có 1 ảnh chính.',
  VALIDATION_ERROR: 'Dữ liệu không hợp lệ.',
};

function describeImageError(error: unknown): string {
  const problem = toProblemDetails(error);
  return IMAGE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? 'Có lỗi xảy ra, vui lòng thử lại.';
}

/**
 * FR-RCP-008 (FE) — upload/sửa metadata/xoá ảnh công thức.
 * Không phụ thuộc GET recipe detail (FR-RCP-002 chưa xong) — chỉ cần recipeId, gọi
 * thẳng 3 endpoint ảnh (D22) và trả kết quả cho component tự cập nhật state của nó.
 */
export function useRecipeImages(recipeId: string) {
  const queryClient = useQueryClient();
  const { show } = useToast();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['recipes'] });
  };

  const uploadMutation = useMutation({
    mutationFn: async ({
      file,
      altText,
      onProgress,
    }: {
      file: File;
      altText?: string;
      onProgress?: (percent: number) => void;
    }) => {
      const form = new FormData();
      form.append('file', file);
      if (altText) {
        form.append('altText', altText);
      }

      // Không tự set header Content-Type: axios phát hiện body là FormData và tự bỏ
      // header mặc định 'application/json' của apiClient để trình duyệt gắn boundary
      // multipart đúng — set tay ở đây sẽ làm mất boundary và server không đọc được file.
      const { data } = await apiClient.post<UploadRecipeImageResponse>(`/recipes/${recipeId}/images`, form, {
        onUploadProgress: (event) => {
          if (onProgress && event.total) {
            onProgress(Math.round((event.loaded / event.total) * 100));
          }
        },
      });

      return data;
    },
    onSuccess: () => {
      invalidate();
      show({ title: 'Tải ảnh thành công', variant: 'success' });
    },
    onError: (error) => {
      show({ title: 'Tải ảnh thất bại', description: describeImageError(error), variant: 'error' });
    },
  });

  const updateMutation = useMutation({
    mutationFn: async ({ imageId, patch }: { imageId: string; patch: UpdateRecipeImagePatch }) => {
      await apiClient.patch(`/recipes/${recipeId}/images/${imageId}`, patch);
    },
    onSuccess: () => {
      invalidate();
      show({ title: 'Đã cập nhật ảnh', variant: 'success' });
    },
    onError: (error) => {
      show({ title: 'Cập nhật ảnh thất bại', description: describeImageError(error), variant: 'error' });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (imageId: string) => {
      await apiClient.delete(`/recipes/${recipeId}/images/${imageId}`);
    },
    onSuccess: () => {
      invalidate();
      show({ title: 'Đã xoá ảnh', variant: 'success' });
    },
    onError: (error) => {
      show({ title: 'Xoá ảnh thất bại', description: describeImageError(error), variant: 'error' });
    },
  });

  return {
    uploadImage: (file: File, altText?: string, onProgress?: (percent: number) => void) =>
      uploadMutation.mutateAsync({ file, altText, onProgress }),
    updateImage: (imageId: string, patch: UpdateRecipeImagePatch) =>
      updateMutation.mutateAsync({ imageId, patch }),
    deleteImage: (imageId: string) => deleteMutation.mutateAsync(imageId),
    isUploading: uploadMutation.isPending,
    isUpdating: updateMutation.isPending,
    isDeleting: deleteMutation.isPending,
  };
}
