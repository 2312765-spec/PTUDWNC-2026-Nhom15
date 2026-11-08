'use client';

import { ArrowDown, ArrowUp, Pencil, Plus, Trash2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Button, Dialog, useToast } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { addIngredient, deleteIngredient, RECIPE_ERROR_MESSAGES, updateIngredient } from '@/lib/recipe-editor-api';
import { toIngredientRequest, type IngredientValues } from '@/lib/recipe-schema';
import type { RecipeIngredientDto } from '@/lib/types';
import { IngredientForm } from './IngredientForm';

export interface RecipeIngredientsManagerProps {
  recipeId: string;
  initialIngredients: RecipeIngredientDto[];
  /** Báo danh sách mới (đã đánh lại orderIndex) cho trang cha cập nhật cache. */
  onChange?: (ingredients: RecipeIngredientDto[]) => void;
}

const sortIngredients = (items: RecipeIngredientDto[]) => [...items].sort((a, b) => a.orderIndex - b.orderIndex);

/** D76 — đánh lại orderIndex 0..n-1 theo thứ tự mảng (giống domain) để cập nhật lạc quan khớp server. */
const reindex = (items: RecipeIngredientDto[]) => items.map((item, i) => ({ ...item, orderIndex: i }));

function toValues(item: RecipeIngredientDto): IngredientValues {
  return {
    name: item.name,
    quantity: item.quantity == null ? '' : String(item.quantity).replace('.', ','),
    unit: item.unit ?? '',
    notes: item.notes ?? '',
  };
}

export function formatQuantity(quantity?: number | null, unit?: string | null) {
  if (quantity == null) return 'vừa đủ';
  return [quantity.toLocaleString('vi-VN'), unit].filter(Boolean).join(' ');
}

function describeError(error: unknown): string {
  const problem = toProblemDetails(error);
  const fieldMessage = Object.values(problem.errors ?? {})[0]?.[0];
  return RECIPE_ERROR_MESSAGES[problem.type] ?? fieldMessage ?? problem.detail ?? 'Có lỗi xảy ra, vui lòng thử lại.';
}

/**
 * FR-RCP-009 — thêm / sửa / xóa / sắp xếp nguyên liệu trên trang sửa công thức, mỗi thao tác một lời gọi API
 * (POST/PUT/DELETE /recipes/{id}/ingredients). Xóa và đổi vị trí cập nhật lạc quan, lỗi thì hoàn tác
 * (NFR-USE-004). Sắp xếp bằng nút Lên/Xuống (bàn phím dùng được — NFR-USE-002). Thao tác chạy tuần tự
 * (khóa nút khi đang gửi) — server cũng khóa dòng recipe (D76).
 */
export function RecipeIngredientsManager({ recipeId, initialIngredients, onChange }: RecipeIngredientsManagerProps) {
  const toast = useToast();
  const [items, setItems] = useState(() => sortIngredients(initialIngredients));
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<RecipeIngredientDto | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const listRef = useRef<HTMLOListElement>(null);
  /** NFR-USE-002 — sau khi đổi chỗ bằng nút Lên/Xuống, đưa focus về nút của nguyên liệu vừa chuyển (giống RecipeStepsManager). */
  const focusAfterMove = useRef<{ id: string; direction: 'up' | 'down' } | null>(null);

  useEffect(() => {
    const target = focusAfterMove.current;
    if (!target) return;
    focusAfterMove.current = null;
    const find = (direction: 'up' | 'down') =>
      listRef.current?.querySelector<HTMLButtonElement>(`[data-move="${target.id}:${direction}"]:not(:disabled)`);
    (find(target.direction) ?? find(target.direction === 'up' ? 'down' : 'up'))?.focus();
  }, [items]);

  function update(next: RecipeIngredientDto[]) {
    setItems(next);
    onChange?.(next);
  }

  async function run(action: () => Promise<void>, rollback?: RecipeIngredientDto[]) {
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

  function handleAdd(values: IngredientValues) {
    void run(async () => {
      const created = await addIngredient(recipeId, toIngredientRequest(values));
      update(sortIngredients([...items, created]));
      setAdding(false);
      setAnnouncement(`Đã thêm nguyên liệu ${created.name}.`);
      toast.show({ variant: 'success', title: 'Đã thêm nguyên liệu', description: created.name });
    });
  }

  function handleEdit(item: RecipeIngredientDto, values: IngredientValues) {
    void run(async () => {
      const updated = await updateIngredient(recipeId, item.id, toIngredientRequest(values));
      update(sortIngredients(updated));
      setEditingId(null);
      toast.show({ variant: 'success', title: 'Đã lưu nguyên liệu', description: values.name.trim() });
    });
  }

  function handleMove(from: number, to: number) {
    if (busy || to < 0 || to >= items.length || from === to) return;
    const previous = items;
    const reordered = [...items];
    const [moved] = reordered.splice(from, 1);
    reordered.splice(to, 0, moved);
    focusAfterMove.current = { id: moved.id, direction: to < from ? 'up' : 'down' };
    update(reindex(reordered));
    setAnnouncement(`Đã chuyển "${moved.name}" tới vị trí ${to + 1}.`);

    void run(async () => {
      const updated = await updateIngredient(recipeId, moved.id, {
        ...toIngredientRequest(toValues(moved)),
        orderIndex: to,
      });
      update(sortIngredients(updated));
    }, previous);
  }

  function confirmDelete() {
    const item = pendingDelete;
    if (!item) return;
    setPendingDelete(null);
    const previous = items;
    update(reindex(items.filter((i) => i.id !== item.id)));
    setAnnouncement(`Đã xóa nguyên liệu "${item.name}".`);

    void run(async () => {
      await deleteIngredient(recipeId, item.id);
      toast.show({ variant: 'success', title: 'Đã xóa nguyên liệu', description: item.name });
    }, previous);
  }

  return (
    <div className="flex flex-col gap-4">
      <p aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {items.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-4 text-sm text-ink-muted">
          Chưa có nguyên liệu nào. Cần ít nhất 1 nguyên liệu để xuất bản.
        </p>
      ) : (
        <ol ref={listRef} className="flex flex-col divide-y divide-border" aria-label="Danh sách nguyên liệu" aria-busy={busy || undefined}>
          {items.map((item, index) => (
            <li key={item.id} className="py-3">
              {editingId === item.id ? (
                <IngredientForm
                  labelPrefix={`Nguyên liệu ${index + 1}`}
                  initialValues={toValues(item)}
                  submitLabel="Lưu nguyên liệu"
                  submitting={busy}
                  onSubmit={(values) => handleEdit(item, values)}
                  onCancel={() => setEditingId(null)}
                />
              ) : (
                <div className="flex items-start gap-3">
                  <div className="min-w-0 flex-1 text-sm">
                    <p className="flex flex-wrap justify-between gap-x-4">
                      <span className="font-medium text-ink">{item.name}</span>
                      <span className="text-ink-muted">{formatQuantity(item.quantity, item.unit)}</span>
                    </p>
                    {item.notes && <p className="mt-0.5 text-ink-muted">{item.notes}</p>}
                  </div>
                  <div className="flex shrink-0 items-start gap-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Chuyển ${item.name} lên`}
                      data-move={`${item.id}:up`}
                      // Đang gửi: aria-disabled chứ không disabled — disabled làm trình duyệt bỏ focus (handleMove tự chặn).
                      aria-disabled={busy || undefined}
                      disabled={index === 0}
                      onClick={() => handleMove(index, index - 1)}
                    >
                      <ArrowUp className="size-4" aria-hidden="true" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Chuyển ${item.name} xuống`}
                      data-move={`${item.id}:down`}
                      aria-disabled={busy || undefined}
                      disabled={index === items.length - 1}
                      onClick={() => handleMove(index, index + 1)}
                    >
                      <ArrowDown className="size-4" aria-hidden="true" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Sửa ${item.name}`}
                      disabled={busy}
                      onClick={() => setEditingId(item.id)}
                    >
                      <Pencil className="size-4" aria-hidden="true" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label={`Xóa ${item.name}`}
                      disabled={busy}
                      onClick={() => setPendingDelete(item)}
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
          <IngredientForm
            labelPrefix="Nguyên liệu mới"
            submitLabel="Thêm nguyên liệu"
            submitting={busy}
            onSubmit={handleAdd}
            onCancel={() => setAdding(false)}
          />
        </div>
      ) : (
        <Button type="button" variant="outline" className="self-start" disabled={busy} onClick={() => setAdding(true)}>
          <Plus className="size-4" aria-hidden="true" /> Thêm nguyên liệu
        </Button>
      )}

      <Dialog
        open={pendingDelete !== null}
        onClose={() => setPendingDelete(null)}
        title="Xóa nguyên liệu này?"
        description={pendingDelete ? `"${pendingDelete.name}" sẽ bị xóa khỏi công thức.` : undefined}
      >
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => setPendingDelete(null)}>
            Hủy
          </Button>
          <Button type="button" variant="danger" onClick={confirmDelete}>
            Xóa nguyên liệu
          </Button>
        </div>
      </Dialog>
    </div>
  );
}
