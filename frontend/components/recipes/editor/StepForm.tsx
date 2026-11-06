'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button, Input, Textarea } from '@/components/ui';
import { emptyStep, stepSchema, type StepValues } from '@/lib/recipe-schema';

export interface StepFormProps {
  /** Dùng làm tiền tố nhãn để các ô có tên riêng khi nhiều form cùng mở (a11y). */
  labelPrefix: string;
  initialValues?: StepValues;
  submitLabel: string;
  submitting?: boolean;
  onSubmit: (values: StepValues) => void;
  onCancel: () => void;
}

/** FR-RCP-010/D6 — form một bước: tiêu đề, mô tả, hẹn giờ. Không có stepNumber (server đánh số). */
export function StepForm({ labelPrefix, initialValues, submitLabel, submitting = false, onSubmit, onCancel }: StepFormProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<StepValues>({ resolver: zodResolver(stepSchema), defaultValues: initialValues ?? emptyStep });

  return (
    <form noValidate onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3">
      <Input label={`${labelPrefix} — tiêu đề`} error={errors.title?.message} {...register('title')} />
      <Textarea label={`${labelPrefix} — mô tả`} rows={3} error={errors.description?.message} {...register('description')} />
      <Input
        label={`${labelPrefix} — hẹn giờ (phút, tùy chọn)`}
        inputMode="numeric"
        className="sm:max-w-40"
        error={errors.timerMinutes?.message}
        {...register('timerMinutes')}
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
