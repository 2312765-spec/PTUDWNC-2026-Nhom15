'use client';

import { ArrowDown, ArrowUp, GripVertical, Pencil, Plus, Timer, Trash2 } from 'lucide-react';
import { useRef, useState } from 'react';
import { Button, Dialog, useToast } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { addStep, deleteStep, RECIPE_ERROR_MESSAGES, updateStep } from '@/lib/recipe-editor-api';
import { toStepRequest, type StepValues } from '@/lib/recipe-schema';
import type { RecipeStepDto } from '@/lib/types';
import { StepForm } from './StepForm';

export interface RecipeStepsManagerProps {
  recipeId: string;
  initialSteps: RecipeStepDto[];
  /** Báo danh sách mới (đã đánh số lại) cho trang cha cập nhật cache. */
  onChange?: (steps: RecipeStepDto[]) => void;
}

const sortSteps = (steps: RecipeStepDto[]) => [...steps].sort((a, b) => a.stepNumber - b.stepNumber);

/** D6 — đánh số lại 1..n theo thứ tự mảng (giống domain) để cập nhật lạc quan khớp với server. */
const renumber = (steps: RecipeStepDto[]) => steps.map((s, i) => ({ ...s, stepNumber: i + 1 }));

function toValues(step: RecipeStepDto): StepValues {
  return {
    title: step.title,
    description: step.description,
    timerMinutes: step.timerMinutes == null ? '' : String(step.timerMinutes),
  };
}

function describeError(error: unknown): string {
  const problem = toProblemDetails(error);
  const fieldMessage = Object.values(problem.errors ?? {})[0]?.[0];
  return RECIPE_ERROR_MESSAGES[problem.type] ?? fieldMessage ?? problem.detail ?? 'Có lỗi xảy ra, vui lòng thử lại.';
}

/**
 * FR-RCP-010 — thêm / sửa / xóa / sắp xếp bước trên trang sửa công thức, mỗi thao tác một lời gọi API
 * (POST/PUT/DELETE /recipes/{id}/steps). Xóa và đổi vị trí cập nhật lạc quan, API lỗi thì hoàn tác
 * (team-assignment: "optimistic update + rollback"). Sắp xếp: kéo-thả, hoặc nút Lên/Xuống cho bàn phím
 * (NFR-USE-002). Các thao tác chạy tuần tự (khóa nút khi đang gửi) — server cũng khóa dòng recipe (D73).
 */
export function RecipeStepsManager({ recipeId, initialSteps, onChange }: RecipeStepsManagerProps) {
  const toast = useToast();
  const [steps, setSteps] = useState(() => sortSteps(initialSteps));
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<RecipeStepDto | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const dragFrom = useRef<number | null>(null);

  /** Cập nhật danh sách và báo trang cha (các thao tác chạy tuần tự nên `steps` luôn là bản mới nhất). */
  function update(next: RecipeStepDto[]) {
    setSteps(next);
    onChange?.(next);
  }

  /** Chạy một thao tác: khóa nút, lỗi thì hoàn tác về `rollback` và báo toast. */
  async function run(action: () => Promise<void>, rollback?: RecipeStepDto[]) {
    setBusy(true);
    try {
      await action();
    } catch (error) {
      if (rollback) update(rollback);
      toast.show({ variant: 'error', title: 'Không lưu được thay đổi', description: describeError(error) });
    } finally {
      setBusy(false);
    }
  }

  function handleAdd(values: StepValues) {
    void run(async () => {
      const created = await addStep(recipeId, toStepRequest(values));
      update(sortSteps([...steps, created]));
      setAdding(false);
      setAnnouncement(`Đã thêm bước ${created.stepNumber}.`);
      toast.show({ variant: 'success', title: 'Đã thêm bước', description: `Bước ${created.stepNumber}: ${created.title}` });
    });
  }

  function handleEdit(step: RecipeStepDto, values: StepValues) {
    void run(async () => {
      const updated = await updateStep(recipeId, step.id, toStepRequest(values));
      update(sortSteps(updated));
      setEditingId(null);
      toast.show({ variant: 'success', title: 'Đã lưu bước', description: `Bước ${step.stepNumber} đã được cập nhật.` });
    });
  }

  function handleMove(from: number, to: number) {
    if (busy || to < 0 || to >= steps.length || from === to) return;
    const previous = steps;
    const reordered = [...steps];
    const [moved] = reordered.splice(from, 1);
    reordered.splice(to, 0, moved);
    update(renumber(reordered));
    setAnnouncement(`Đã chuyển "${moved.title}" tới vị trí ${to + 1}.`);

    void run(async () => {
      const updated = await updateStep(recipeId, moved.id, { ...toStepRequest(toValues(moved)), stepNumber: to + 1 });
      update(sortSteps(updated));
    }, previous);
  }

  function confirmDelete() {
    const step = pendingDelete;
    if (!step) return;
    setPendingDelete(null);
    const previous = steps;
    update(renumber(steps.filter((s) => s.id !== step.id)));
    setAnnouncement(`Đã xóa bước "${step.title}".`);

    void run(async () => {
      await deleteStep(recipeId, step.id);
      toast.show({ variant: 'success', title: 'Đã xóa bước', description: step.title });
    }, previous);
  }

  return (
    <div className="flex flex-col gap-4">
      <p aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {steps.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-4 text-sm text-ink-muted">
          Chưa có bước nào. Cần ít nhất 1 bước để xuất bản.
        </p>
      ) : (
        <ol className="flex flex-col gap-3" aria-label="Các bước thực hiện" aria-busy={busy || undefined}>
          {steps.map((step, index) => (
            <li
              key={step.id}
              draggable={!busy && editingId === null}
              onDragStart={() => {
                dragFrom.current = index;
              }}
              onDragOver={(event) => event.preventDefault()}
              onDrop={(event) => {
                event.preventDefault();
                if (dragFrom.current !== null) handleMove(dragFrom.current, index);
                dragFrom.current = null;
              }}
              className="rounded-md border border-border bg-surface p-4"
            >
              {editingId === step.id ? (
                <StepForm
                  labelPrefix={`Bước ${step.stepNumber}`}
                  initialValues={toValues(step)}
                  submitLabel="Lưu bước"
                  submitting={busy}
                  onSubmit={(values) => handleEdit(step, values)}
                  onCancel={() => setEditingId(null)}
                />
              ) : (
                <div className="flex flex-wrap sm:flex-nowrap gap-3">
                  <GripVertical className="mt-1 size-4 shrink-0 cursor-grab text-ink-muted" aria-hidden="true" />
                  <div className="min-w-0 flex-1">
                    <h3 className="font-semibold text-ink">
                      Bước {step.stepNumber}: {step.title}
                    </h3>
                    <p className="mt-1 whitespace-pre-line text-sm text-ink-muted">{step.description}</p>
                    {step.timerMinutes != null && (
                      <p className="mt-1 flex items-center gap-1 text-xs text-ink-muted">
                        <Timer className="size-3.5" aria-hidden="true" /> {step.timerMinutes} phút
                      </p>
                    )}
                  </div>
                  {/* D84 — dưới 640px nhóm nút xuống hàng riêng, không ép chữ thành cột hẹp. */}
                  <div className="flex shrink-0 basis-full items-start justify-end gap-1 sm:basis-auto">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Chuyển bước ${step.stepNumber} lên`}
                      disabled={busy || index === 0}
                      onClick={() => handleMove(index, index - 1)}
                    >
                      <ArrowUp className="size-4" aria-hidden="true" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Chuyển bước ${step.stepNumber} xuống`}
                      disabled={busy || index === steps.length - 1}
                      onClick={() => handleMove(index, index + 1)}
                    >
                      <ArrowDown className="size-4" aria-hidden="true" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Sửa bước ${step.stepNumber}`}
                      disabled={busy}
                      onClick={() => setEditingId(step.id)}
                    >
                      <Pencil className="size-4" aria-hidden="true" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Xóa bước ${step.stepNumber}`}
                      disabled={busy}
                      onClick={() => setPendingDelete(step)}
                    >
                      <Trash2 className="size-4" aria-hidden="true" />
                    </Button>
                  </div>
                </div>
              )}
            </li>
          ))}
        </ol>
      )}

      {adding ? (
        <div className="rounded-md border border-brand-600 bg-surface p-4">
          <StepForm
            labelPrefix="Bước mới"
            submitLabel="Thêm bước"
            submitting={busy}
            onSubmit={handleAdd}
            onCancel={() => setAdding(false)}
          />
        </div>
      ) : (
        <Button type="button" variant="outline" className="self-start" disabled={busy} onClick={() => setAdding(true)}>
          <Plus className="size-4" aria-hidden="true" /> Thêm bước
        </Button>
      )}

      <Dialog
        open={pendingDelete !== null}
        onClose={() => setPendingDelete(null)}
        title="Xóa bước này?"
        description={
          pendingDelete
            ? `Bước ${pendingDelete.stepNumber} "${pendingDelete.title}" sẽ bị xóa, các bước sau được đánh số lại.`
            : undefined
        }
      >
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => setPendingDelete(null)}>
            Hủy
          </Button>
          <Button type="button" variant="danger" onClick={confirmDelete}>
            Xóa bước
          </Button>
        </div>
      </Dialog>
    </div>
  );
}
