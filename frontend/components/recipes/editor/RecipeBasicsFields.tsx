'use client';

import type { FieldErrors, FieldValues, Path, UseFormRegister } from 'react-hook-form';
import { Input, Select, Skeleton, Textarea } from '@/components/ui';
import { difficultyOptions, type RecipeBasicsValues } from '@/lib/recipe-schema';
import type { CategoryDto } from '@/lib/types';

export interface RecipeBasicsFieldsProps<T extends FieldValues & RecipeBasicsValues> {
  register: UseFormRegister<T>;
  errors: FieldErrors<T>;
  categories: CategoryDto[] | undefined;
  categoriesLoading?: boolean;
}

function message(errors: FieldErrors, name: keyof RecipeBasicsValues): string | undefined {
  const value = errors[name]?.message;
  return typeof value === 'string' ? value : undefined;
}

/**
 * FR-RCP-003 (bước 1 của wizard) và FR-RCP-004 (form sửa) — các trường thông tin chung (D69).
 * Slug không có ở đây: server sinh từ tiêu đề (D10) và không bao giờ đổi (D13).
 */
export function RecipeBasicsFields<T extends FieldValues & RecipeBasicsValues>({
  register,
  errors,
  categories,
  categoriesLoading = false,
}: RecipeBasicsFieldsProps<T>) {
  const field = (name: keyof RecipeBasicsValues) => register(name as Path<T>);
  const err = (name: keyof RecipeBasicsValues) => message(errors as FieldErrors, name);

  return (
    <div className="flex flex-col gap-4">
      <Input label="Tiêu đề" placeholder="Ví dụ: Gỏi cuốn tôm thịt" error={err('title')} {...field('title')} />

      <Textarea
        label="Mô tả ngắn"
        rows={3}
        hint="Giới thiệu món ăn trong vài câu (tối đa 2000 ký tự)."
        error={err('description')}
        {...field('description')}
      />

      {categoriesLoading ? (
        <div aria-busy="true" aria-label="Đang tải danh mục" className="flex flex-col gap-1.5">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : (
        <Select
          label="Danh mục"
          placeholder="Chọn danh mục"
          options={(categories ?? []).map((c) => ({ value: c.id, label: c.name }))}
          error={err('categoryId')}
          {...field('categoryId')}
        />
      )}

      <div className="grid gap-4 sm:grid-cols-3">
        <Input
          label="Chuẩn bị (phút)"
          inputMode="numeric"
          error={err('prepTime')}
          {...field('prepTime')}
        />
        <Input
          label="Nấu (phút)"
          inputMode="numeric"
          hint="0 nếu không cần nấu."
          error={err('cookTime')}
          {...field('cookTime')}
        />
        <Input label="Khẩu phần" inputMode="numeric" error={err('servings')} {...field('servings')} />
      </div>

      <Select
        label="Độ khó"
        options={difficultyOptions.map((o) => ({ value: o.value, label: o.label }))}
        error={err('difficulty')}
        {...field('difficulty')}
      />

      <Textarea
        label="Ghi chú chung (tùy chọn)"
        rows={3}
        hint="Mẹo, lưu ý chung. Các bước chi tiết nhập ở phần Các bước thực hiện."
        error={err('instructions')}
        {...field('instructions')}
      />
    </div>
  );
}
