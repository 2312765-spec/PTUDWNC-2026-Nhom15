'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button, Input } from '@/components/ui';
import { emptyIngredient, ingredientSchema, type IngredientValues } from '@/lib/recipe-schema';

export interface IngredientFormProps {
  /** Tiền tố nhãn để các ô có tên riêng khi nhiều form cùng mở (a11y). */
  labelPrefix: string;
  initialValues?: IngredientValues;
  submitLabel: string;
  submitting?: boolean;
  onSubmit: (values: IngredientValues) => void;
  onCancel: () => void;
}

/** FR-RCP-009/D7 — form một nguyên liệu: tên bắt buộc; số lượng/đơn vị để trống = "vừa đủ". */
export function IngredientForm({
  labelPrefix,
  initialValues,
  submitLabel,
  submitting = false,
  onSubmit,
  onCancel,
}: IngredientFormProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<IngredientValues>({
    resolver: zodResolver(ingredientSchema),
    defaultValues: initialValues ?? emptyIngredient,
  });

  return (
    <form noValidate onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3">
      <div className="grid gap-3 sm:grid-cols-[2fr_1fr_1fr]">
        <Input label={`${labelPrefix} — tên`} placeholder="Ví dụ: Tôm sú" error={errors.name?.message} {...register('name')} />
        <Input
          label={`${labelPrefix} — số lượng`}
          inputMode="decimal"
          placeholder="Để trống = vừa đủ"
          error={errors.quantity?.message}
          {...register('quantity')}
        />
        <Input
          label={`${labelPrefix} — đơn vị`}
          placeholder="g, ml, quả…"
          error={errors.unit?.message}
          {...register('unit')}
        />
      </div>
      <Input
        label={`${labelPrefix} — ghi chú (tùy chọn)`}
        placeholder="Ví dụ: bóc vỏ, bỏ chỉ lưng"
        error={errors.notes?.message}
        {...register('notes')}
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" size="sm" onClick={onCancel}>
          Hủy
        </Button>
        <Button type="submit" size="sm" loading={submitting}>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
