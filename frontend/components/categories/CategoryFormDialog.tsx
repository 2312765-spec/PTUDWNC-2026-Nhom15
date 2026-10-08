'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Button, Dialog, Input, Textarea, useToast } from '@/components/ui';
import { apiClient, toProblemDetails } from '@/lib/api-client';
import { categorySchema, toCategoryRequest, type CategoryFormValues } from '@/lib/category-schema';
import type { CategoryDto } from '@/lib/types';

const formFields = ['name', 'description'] as const;
type FormField = (typeof formFields)[number];

/** Backend trả key theo PropertyName của FluentValidation ("Name", "Description"). */
function toFormField(key: string): FormField | undefined {
  return formFields.find((f) => f.toLowerCase() === key.toLowerCase());
}

export interface CategoryFormDialogProps {
  open: boolean;
  onClose: () => void;
  /** null = tạo mới (FR-CAT-003); có giá trị = sửa (FR-CAT-004). */
  category: CategoryDto | null;
  onSaved: () => void;
}

/**
 * FR-CAT-003 (tạo) / FR-CAT-004 (sửa — D10: slug giữ nguyên, chỉ đổi tên/mô tả). Admin.
 * NFR-USE-003: lỗi validation inline theo field, 409 CATEGORY_NAME_EXISTS gắn vào ô tên.
 * NFR-USE-004: nút loading, toast sau khi lưu.
 */
export function CategoryFormDialog({ open, onClose, category, onSaved }: CategoryFormDialogProps) {
  const toast = useToast();
  const [formError, setFormError] = useState<string | null>(null);
  const isEdit = category !== null;

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: '', description: '' },
  });

  useEffect(() => {
    if (open) {
      reset({ name: category?.name ?? '', description: category?.description ?? '' });
    }
  }, [open, category, reset]);

  const handleClose = () => {
    setFormError(null);
    onClose();
  };

  const mutation = useMutation({
    onMutate: () => setFormError(null),
    mutationFn: (values: CategoryFormValues) =>
      isEdit
        ? apiClient.put(`/categories/${category.id}`, toCategoryRequest(values))
        : apiClient.post('/categories', toCategoryRequest(values)),
    onSuccess: (_, values) => {
      toast.show({
        variant: 'success',
        title: isEdit ? 'Đã cập nhật danh mục' : 'Đã tạo danh mục',
        description: `Danh mục "${values.name}" đã được lưu.`,
      });
      onSaved();
      handleClose();
    },
    onError: (err) => {
      const problem = toProblemDetails(err);

      if (problem.type === 'CATEGORY_NAME_EXISTS') {
        setError('name', { message: 'Tên danh mục này đã tồn tại.' });
        return;
      }

      if (problem.errors) {
        let mapped = false;
        for (const [key, messages] of Object.entries(problem.errors)) {
          const field = toFormField(key);
          if (field && messages[0]) {
            setError(field, { message: messages[0] });
            mapped = true;
          }
        }
        if (mapped) return;
      }

      setFormError(problem.detail ?? problem.title);
    },
  });

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      title={isEdit ? 'Chỉnh sửa danh mục' : 'Tạo danh mục mới'}
      description={isEdit ? 'Đường dẫn (slug) giữ nguyên khi đổi tên để không gãy liên kết.' : undefined}
    >
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate className="flex flex-col gap-4">
        {formError && (
          <p role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            {formError}
          </p>
        )}

        <Input
          label="Tên danh mục"
          placeholder="Ví dụ: Món ăn sáng"
          error={errors.name?.message}
          {...register('name')}
        />

        <Textarea
          label="Mô tả (tùy chọn)"
          rows={3}
          error={errors.description?.message}
          {...register('description')}
        />

        <div className="flex justify-end gap-2 border-t border-border pt-4">
          <Button type="button" variant="outline" onClick={handleClose}>
            Hủy
          </Button>
          <Button type="submit" loading={mutation.isPending}>
            {isEdit ? 'Lưu thay đổi' : 'Tạo mới'}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
