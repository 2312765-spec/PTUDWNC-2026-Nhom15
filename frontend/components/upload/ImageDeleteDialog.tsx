'use client';

import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';

export interface ImageDeleteDialogProps {
  open: boolean;
  onCancel: () => void;
  onConfirm: () => void;
  isDeleting?: boolean;
}

export function ImageDeleteDialog({ open, onCancel, onConfirm, isDeleting = false }: ImageDeleteDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title="Xoá ảnh này?"
      description="Không thể khôi phục. Nếu đây là ảnh chính, ảnh có thứ tự nhỏ nhất kế tiếp sẽ tự động lên thay (D22)."
    >
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          Huỷ
        </Button>
        <Button type="button" variant="danger" loading={isDeleting} onClick={onConfirm}>
          Xoá ảnh
        </Button>
      </div>
    </Dialog>
  );
}
