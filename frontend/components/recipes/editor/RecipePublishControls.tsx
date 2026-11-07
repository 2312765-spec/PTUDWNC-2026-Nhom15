'use client';

import { Archive, Eye, EyeOff, Trash2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Dialog, useToast } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { archiveRecipe, deleteRecipe, RECIPE_ERROR_MESSAGES, setRecipePublished } from '@/lib/recipe-editor-api';
import { RecipeStatus, type RecipeDetailDto } from '@/lib/types';

export interface RecipePublishControlsProps {
  recipe: RecipeDetailDto;
  /** Đổi trạng thái thành công → DTO mới (rowVersion mới — D77); trang cha PHẢI ghi vào cache. */
  onChange: (recipe: RecipeDetailDto) => void;
}

function describeError(error: unknown): string {
  const problem = toProblemDetails(error);
  return RECIPE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? 'Có lỗi xảy ra, vui lòng thử lại.';
}

/**
 * FR-RCP-005 (publish / unpublish — D3, D77) + FR-RCP-006 (lưu trữ — D79) + FR-RCP-007 (xóa mềm — D1, D78)
 * trên trang sửa công thức. Nút "Xuất bản" bị khóa khi còn thiếu nguyên liệu hoặc bước (D3) và nói rõ thiếu gì —
 * backend vẫn tự kiểm tra. Lưu trữ và xóa cần xác nhận qua hộp thoại; xóa xong thì về /dashboard (công thức đã xóa
 * không mở lại được — 404). Recipe Archived thoát ra bằng "Xuất bản" / "Chuyển về nháp" (D77).
 */
export function RecipePublishControls({ recipe, onChange }: RecipePublishControlsProps) {
  const toast = useToast();
  const router = useRouter();
  const [busy, setBusy] = useState<'publish' | 'archive' | 'delete' | null>(null);
  const [confirmingArchive, setConfirmingArchive] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const isPublished = recipe.status === RecipeStatus.Published;
  const isArchived = recipe.status === RecipeStatus.Archived;
  const missing = [
    recipe.ingredients.length === 0 && '1 nguyên liệu',
    recipe.steps.length === 0 && '1 bước thực hiện',
  ].filter(Boolean);
  const canPublish = missing.length === 0;

  async function changeStatus(publish: boolean) {
    setBusy('publish');
    try {
      const updated = await setRecipePublished(recipe.id, publish);
      onChange(updated);
      toast.show({
        variant: 'success',
        title: publish ? 'Đã xuất bản' : 'Đã chuyển về nháp',
        description: publish ? `"${updated.title}" đã hiển thị công khai.` : `"${updated.title}" không còn hiển thị công khai.`,
      });
    } catch (error) {
      toast.show({ variant: 'error', title: 'Không đổi được trạng thái', description: describeError(error) });
    } finally {
      setBusy(null);
    }
  }

  async function confirmArchive() {
    setConfirmingArchive(false);
    setBusy('archive');
    try {
      const updated = await archiveRecipe(recipe.id);
      onChange(updated);
      toast.show({ variant: 'success', title: 'Đã lưu trữ', description: `"${updated.title}" đã ẩn khỏi trang công khai.` });
    } catch (error) {
      toast.show({ variant: 'error', title: 'Không lưu trữ được công thức', description: describeError(error) });
    } finally {
      setBusy(null);
    }
  }

  async function confirmDelete() {
    setConfirmingDelete(false);
    setBusy('delete');
    try {
      await deleteRecipe(recipe.id);
      toast.show({ variant: 'success', title: 'Đã xóa công thức', description: recipe.title });
      router.push('/dashboard');
    } catch (error) {
      toast.show({ variant: 'error', title: 'Không xóa được công thức', description: describeError(error) });
      setBusy(null);
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        {isPublished ? (
          <Button type="button" variant="outline" loading={busy === 'publish'} disabled={busy !== null} onClick={() => changeStatus(false)}>
            <EyeOff className="size-4" aria-hidden="true" /> Hủy xuất bản
          </Button>
        ) : (
          <Button
            type="button"
            loading={busy === 'publish'}
            disabled={busy !== null || !canPublish}
            aria-describedby={canPublish ? undefined : 'publish-hint'}
            onClick={() => changeStatus(true)}
          >
            <Eye className="size-4" aria-hidden="true" /> Xuất bản
          </Button>
        )}
        {isArchived && (
          <Button type="button" variant="outline" disabled={busy !== null} onClick={() => changeStatus(false)}>
            Chuyển về nháp
          </Button>
        )}
        {!isArchived && (
          <Button
            type="button"
            variant="outline"
            loading={busy === 'archive'}
            disabled={busy !== null}
            onClick={() => setConfirmingArchive(true)}
          >
            <Archive className="size-4" aria-hidden="true" /> Lưu trữ
          </Button>
        )}
        <Button
          type="button"
          variant="danger"
          loading={busy === 'delete'}
          disabled={busy !== null}
          onClick={() => setConfirmingDelete(true)}
        >
          <Trash2 className="size-4" aria-hidden="true" /> Xóa công thức
        </Button>
      </div>
      {!isPublished && !canPublish && (
        <p id="publish-hint" className="text-sm text-ink-muted">
          Cần thêm ít nhất {missing.join(' và ')} để xuất bản.
        </p>
      )}

      <Dialog
        open={confirmingArchive}
        onClose={() => setConfirmingArchive(false)}
        title="Lưu trữ công thức này?"
        description={`"${recipe.title}" sẽ ẩn khỏi trang công khai và kết quả tìm kiếm. Dữ liệu vẫn giữ nguyên — bạn có thể xuất bản lại hoặc chuyển về nháp bất cứ lúc nào.`}
      >
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => setConfirmingArchive(false)}>
            Hủy
          </Button>
          <Button type="button" onClick={confirmArchive}>
            Lưu trữ
          </Button>
        </div>
      </Dialog>

      <Dialog
        open={confirmingDelete}
        onClose={() => setConfirmingDelete(false)}
        title="Xóa công thức này?"
        description={`"${recipe.title}" sẽ bị gỡ khỏi trang web và bảng điều khiển của bạn. Đường dẫn /recipes/${recipe.slug} sẽ không dùng lại được.`}
      >
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => setConfirmingDelete(false)}>
            Hủy
          </Button>
          <Button type="button" variant="danger" onClick={confirmDelete}>
            Xóa công thức
          </Button>
        </div>
      </Dialog>
    </div>
  );
}
