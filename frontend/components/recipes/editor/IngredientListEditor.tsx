'use client';

import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form';
import { Button, Input } from '@/components/ui';
import { emptyIngredient, MAX_CHILDREN, type CreateRecipeValues } from '@/lib/recipe-schema';

export interface IngredientListEditorProps {
  control: Control<CreateRecipeValues>;
  register: UseFormRegister<CreateRecipeValues>;
  errors: FieldErrors<CreateRecipeValues>;
}

/**
 * FR-RCP-003 bước 2 của wizard — form động nguyên liệu (D7: số lượng/đơn vị để trống cho "vừa đủ").
 * Gửi kèm POST /recipes; sửa riêng từng nguyên liệu sau khi tạo thuộc FR-RCP-009.
 */
export function IngredientListEditor({ control, register, errors }: IngredientListEditorProps) {
  const { fields, append, remove } = useFieldArray({ control, name: 'ingredients' });

  return (
    <div className="flex flex-col gap-4">
      {fields.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-4 text-sm text-ink-muted">
          Chưa có nguyên liệu. Có thể lưu nháp trước, nhưng cần ít nhất 1 nguyên liệu để xuất bản.
        </p>
      ) : (
        <ol className="flex flex-col gap-3" aria-label="Danh sách nguyên liệu">
          {fields.map((item, index) => {
            const e = errors.ingredients?.[index];
            return (
              <li key={item.id} className="rounded-md border border-border bg-surface p-3">
                <fieldset className="grid gap-3 sm:grid-cols-[2fr_1fr_1fr_auto] sm:items-start">
                  <legend className="sr-only">Nguyên liệu {index + 1}</legend>
                  <Input
                    label={`Tên nguyên liệu ${index + 1}`}
                    placeholder="Ví dụ: Tôm sú"
                    error={e?.name?.message}
                    {...register(`ingredients.${index}.name`)}
                  />
                  <Input
                    label="Số lượng"
                    inputMode="decimal"
                    placeholder="Để trống = vừa đủ"
                    error={e?.quantity?.message}
                    {...register(`ingredients.${index}.quantity`)}
                  />
                  <Input
                    label="Đơn vị"
                    placeholder="g, ml, quả…"
                    error={e?.unit?.message}
                    {...register(`ingredients.${index}.unit`)}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="sm:mt-7"
                    aria-label={`Xóa nguyên liệu ${index + 1}`}
                    onClick={() => remove(index)}
                  >
                    <Trash2 className="size-4" aria-hidden="true" />
                  </Button>
                  <div className="sm:col-span-4">
                    <Input
                      label="Ghi chú (tùy chọn)"
                      placeholder="Ví dụ: bóc vỏ, bỏ chỉ lưng"
                      error={e?.notes?.message}
                      {...register(`ingredients.${index}.notes`)}
                    />
                  </div>
                </fieldset>
              </li>
            );
          })}
        </ol>
      )}

      <Button
        type="button"
        variant="outline"
        className="self-start"
        disabled={fields.length >= MAX_CHILDREN}
        onClick={() => append({ ...emptyIngredient })}
      >
        <Plus className="size-4" aria-hidden="true" /> Thêm nguyên liệu
      </Button>
    </div>
  );
}
