'use client';

import { ArrowDown, ArrowUp, GripVertical, Plus, Trash2 } from 'lucide-react';
import { useRef, useState } from 'react';
import { useFieldArray, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form';
import { Button, Input, Textarea } from '@/components/ui';
import { emptyStep, MAX_CHILDREN, type CreateRecipeValues } from '@/lib/recipe-schema';

export interface StepListEditorProps {
  control: Control<CreateRecipeValues>;
  register: UseFormRegister<CreateRecipeValues>;
  errors: FieldErrors<CreateRecipeValues>;
}

/**
 * FR-RCP-003 bước 3 của wizard — form động các bước. Số thứ tự do server đánh theo thứ tự gửi (D6),
 * nên sắp xếp ở đây chỉ là đổi thứ tự trong mảng: kéo-thả bằng chuột, hoặc nút Lên/Xuống cho bàn
 * phím và trình đọc màn hình (NFR-USE-002).
 */
export function StepListEditor({ control, register, errors }: StepListEditorProps) {
  const { fields, append, remove, move } = useFieldArray({ control, name: 'steps' });
  const dragFrom = useRef<number | null>(null);
  const [announcement, setAnnouncement] = useState('');

  function moveStep(from: number, to: number) {
    if (to < 0 || to >= fields.length || from === to) return;
    move(from, to);
    setAnnouncement(`Đã chuyển bước ${from + 1} tới vị trí ${to + 1}.`);
  }

  return (
    <div className="flex flex-col gap-4">
      <p aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {fields.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-4 text-sm text-ink-muted">
          Chưa có bước nào. Có thể lưu nháp trước, nhưng cần ít nhất 1 bước để xuất bản.
        </p>
      ) : (
        <ol className="flex flex-col gap-3" aria-label="Danh sách các bước">
          {fields.map((item, index) => {
            const e = errors.steps?.[index];
            return (
              <li
                key={item.id}
                draggable
                onDragStart={() => {
                  dragFrom.current = index;
                }}
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => {
                  event.preventDefault();
                  if (dragFrom.current !== null) moveStep(dragFrom.current, index);
                  dragFrom.current = null;
                }}
                className="rounded-md border border-border bg-surface p-3"
              >
                <fieldset className="flex flex-col gap-3">
                  <legend className="flex w-full items-center gap-2 text-sm font-semibold text-ink">
                    <GripVertical className="size-4 cursor-grab text-ink-muted" aria-hidden="true" />
                    Bước {index + 1}
                    <span className="ml-auto flex gap-1">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        aria-label={`Chuyển bước ${index + 1} lên`}
                        disabled={index === 0}
                        onClick={() => moveStep(index, index - 1)}
                      >
                        <ArrowUp className="size-4" aria-hidden="true" />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        aria-label={`Chuyển bước ${index + 1} xuống`}
                        disabled={index === fields.length - 1}
                        onClick={() => moveStep(index, index + 1)}
                      >
                        <ArrowDown className="size-4" aria-hidden="true" />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        aria-label={`Xóa bước ${index + 1}`}
                        onClick={() => remove(index)}
                      >
                        <Trash2 className="size-4" aria-hidden="true" />
                      </Button>
                    </span>
                  </legend>
                  <Input
                    label={`Tiêu đề bước ${index + 1}`}
                    placeholder="Ví dụ: Sơ chế tôm"
                    error={e?.title?.message}
                    {...register(`steps.${index}.title`)}
                  />
                  <Textarea
                    label="Mô tả"
                    rows={3}
                    error={e?.description?.message}
                    {...register(`steps.${index}.description`)}
                  />
                  <Input
                    label="Hẹn giờ (phút, tùy chọn)"
                    inputMode="numeric"
                    className="sm:max-w-40"
                    error={e?.timerMinutes?.message}
                    {...register(`steps.${index}.timerMinutes`)}
                  />
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
        onClick={() => append({ ...emptyStep })}
      >
        <Plus className="size-4" aria-hidden="true" /> Thêm bước
      </Button>
    </div>
  );
}
