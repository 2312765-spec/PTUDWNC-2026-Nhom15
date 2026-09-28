'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Dialog, useToast } from '@/components/ui';
import { apiClient, toProblemDetails } from '@/lib/api-client';

export interface DeleteCategoryButtonProps {
  categoryId: string;
  categoryName: string;
  recipeCount?: number;
}

/**
 * Nút xóa danh mục (FR-CAT-005/D2 — soft delete, 409 CATEGORY_DELETE_HAS_RECIPES nếu còn
 * công thức). Chỉ Admin mới xóa được — backend tự chặn 403 qua Policies.Admin; nút này
 * chưa tự ẩn theo role vì FE chưa có Auth.js (xem TODO(S2 — A) trong lib/api-client.ts).
 */
export function DeleteCategoryButton({ categoryId, categoryName, recipeCount = 0 }: DeleteCategoryButtonProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const router = useRouter();
  const toast = useToast();

  const handleConfirm = async () => {
    setIsDeleting(true);
    try {
      // baseURL của apiClient đã có sẵn /api/v1 — không lặp lại ở đây.
      await apiClient.delete(`/categories/${categoryId}`);
      toast.show({
        variant: 'success',
        title: 'Đã xóa danh mục',
        description: `Danh mục "${categoryName}" đã được xóa.`,
      });
      setIsOpen(false);
      router.refresh();
    } catch (err) {
      const problem = toProblemDetails(err);
      toast.show({
        variant: 'error',
        title: problem.type === 'CATEGORY_DELETE_HAS_RECIPES' ? 'Danh mục còn công thức' : 'Không thể xóa danh mục',
        description: problem.detail ?? problem.title,
      });
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <>
      <Button type="button" variant="danger" size="sm" onClick={() => setIsOpen(true)}>
        Xóa
      </Button>

      <Dialog
        open={isOpen}
        onClose={() => setIsOpen(false)}
        title="Xác nhận xóa danh mục"
        description={
          recipeCount > 0
            ? `Danh mục "${categoryName}" còn ${recipeCount} công thức — phải chuyển hết sang danh mục khác trước khi xóa.`
            : `Bạn có chắc muốn xóa danh mục "${categoryName}"? Đây là xóa mềm (D2), danh mục sẽ không còn hiển thị trong danh sách.`
        }
      >
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => setIsOpen(false)}>
            Hủy
          </Button>
          <Button
            type="button"
            variant="danger"
            loading={isDeleting}
            disabled={recipeCount > 0}
            onClick={handleConfirm}
          >
            Xác nhận xóa
          </Button>
        </div>
      </Dialog>
    </>
  );
}
