import { z } from 'zod';

/**
 * FR-CAT-003/FR-CAT-004 — khớp Create/UpdateCategoryCommandValidator của backend.
 * Validate ở client chỉ để phản hồi nhanh — backend vẫn là nguồn quyết định (CONS-008).
 */
export const categorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, 'Tên danh mục phải từ 2 đến 50 ký tự.')
    .max(50, 'Tên danh mục phải từ 2 đến 50 ký tự.'),
  description: z.string().trim().max(500, 'Mô tả không được vượt quá 500 ký tự.'),
});

export type CategoryFormValues = z.infer<typeof categorySchema>;

/** Body gửi lên POST/PUT /categories — mô tả rỗng gửi null. */
export function toCategoryRequest(values: CategoryFormValues) {
  return { name: values.name, description: values.description || null };
}
