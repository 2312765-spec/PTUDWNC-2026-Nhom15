'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, ShieldAlert } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { RecipeBasicsForm } from '@/components/recipes/editor/RecipeBasicsForm';
import { RecipeIngredientsManager } from '@/components/recipes/editor/RecipeIngredientsManager';
import { RecipePublishControls } from '@/components/recipes/editor/RecipePublishControls';
import { RecipeStepsManager } from '@/components/recipes/editor/RecipeStepsManager';
import { Badge, Button, Skeleton } from '@/components/ui';
import { RecipeImageManager } from '@/components/upload/RecipeImageManager';
import { toProblemDetails } from '@/lib/api-client';
import { fetchRecipeById, RECIPE_ERROR_MESSAGES, recipeEditorKeys } from '@/lib/recipe-editor-api';
import { RecipeStatus, type RecipeDetailDto, type RecipeImageDto } from '@/lib/types';

const STATUS_LABEL: Record<number, { label: string; variant: 'default' | 'success' | 'warning' }> = {
  [RecipeStatus.Draft]: { label: 'Nháp', variant: 'warning' },
  [RecipeStatus.Published]: { label: 'Đã xuất bản', variant: 'success' },
  [RecipeStatus.Archived]: { label: 'Đã lưu trữ', variant: 'default' },
};

/** FR-RCP-008 (của D) nhận RecipeImageDto (D27) — chuyển từ ảnh trong RecipeDetailDto. */
function toImageDtos(recipe: RecipeDetailDto): RecipeImageDto[] {
  return [...recipe.images]
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .map((img) => ({
      imageId: img.id,
      originalUrl: img.originalUrl,
      mediumUrl: null,
      thumbnailUrl: null,
      altText: img.altText ?? null,
      isPrimary: img.isPrimary,
      orderIndex: img.orderIndex,
    }));
}

/**
 * Trang sửa công thức — FR-RCP-004 (thông tin chung, If-Match — D71), FR-RCP-005 (xuất bản — D77),
 * FR-RCP-006 (lưu trữ — D79), FR-RCP-007 (xóa — D78), FR-RCP-009 (nguyên liệu — D76), FR-RCP-010 (các bước), FR-RCP-008 (ảnh, component của D). Đọc theo id qua GET /recipes/{id} (D75), không cache để luôn có
 * rowVersion mới nhất. Quyền: chỉ owner hoặc Admin (backend trả 403 RECIPE_FORBIDDEN). SRS 5.1: CSR.
 */
export default function EditRecipePage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const queryKey = recipeEditorKeys.recipe(id);

  const { data: recipe, isPending, isError, error, refetch } = useQuery({
    queryKey,
    queryFn: () => fetchRecipeById(id),
    staleTime: 0,
    retry: false,
  });

  const setRecipe = (next: RecipeDetailDto) => queryClient.setQueryData(queryKey, next);

  if (isPending) {
    return (
      <div aria-busy="true" aria-label="Đang tải công thức" className="mx-auto flex max-w-3xl flex-col gap-4 px-4 py-10">
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (isError) {
    const problem = toProblemDetails(error);
    const forbidden = problem.status === 403 || problem.type === 'RECIPE_FORBIDDEN';
    return (
      <div role="alert" className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
        <ShieldAlert className="size-10 text-danger" aria-hidden="true" />
        <h1 className="mt-3 text-xl font-bold text-ink">
          {forbidden ? 'Bạn không có quyền sửa công thức này' : problem.status === 404 ? 'Không tìm thấy công thức' : 'Không tải được công thức'}
        </h1>
        <p className="mt-2 text-sm text-ink-muted">
          {RECIPE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? 'Vui lòng thử lại.'}
        </p>
        <div className="mt-6 flex gap-2">
          {!forbidden && problem.status !== 404 && (
            <Button type="button" variant="outline" onClick={() => refetch()}>
              Thử lại
            </Button>
          )}
          <Link
            href="/dashboard"
            className="inline-flex h-10 items-center rounded-md bg-brand-600 px-4 text-sm font-medium text-on-brand hover:bg-brand-700"
          >
            Về Bảng điều khiển
          </Link>
        </div>
      </div>
    );
  }

  const status = STATUS_LABEL[recipe.status] ?? STATUS_LABEL[RecipeStatus.Draft];

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <header className="flex flex-col gap-3 border-b border-border pb-6">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-2xl font-bold text-brand-700">Sửa công thức</h1>
          <Badge variant={status.variant}>{status.label}</Badge>
        </div>
        <p className="text-sm text-ink-muted">
          {recipe.title} · đường dẫn <code className="font-mono text-xs">/recipes/{recipe.slug}</code> (giữ nguyên khi đổi tiêu đề)
        </p>
        <Link
          href={`/recipes/${recipe.slug}`}
          className="inline-flex min-h-6 items-center gap-1 self-start text-sm font-medium text-brand-700 underline-offset-2 hover:underline"
        >
          Xem trang công thức <ExternalLink className="size-3.5" aria-hidden="true" />
        </Link>
        <RecipePublishControls recipe={recipe} onChange={setRecipe} />
      </header>

      <section aria-labelledby="basics-heading" className="mt-8 rounded-lg border border-border bg-surface p-6">
        <h2 id="basics-heading" className="mb-4 text-lg font-semibold text-ink">
          Thông tin chung
        </h2>
        <RecipeBasicsForm
          recipe={recipe}
          onSaved={setRecipe}
          onReload={async () => (await refetch()).data}
        />
      </section>

      <section aria-labelledby="ingredients-heading" className="mt-8 rounded-lg border border-border bg-surface p-6">
        <h2 id="ingredients-heading" className="text-lg font-semibold text-ink">
          Nguyên liệu ({recipe.ingredients.length})
        </h2>
        <p className="mb-4 mt-1 text-sm text-ink-muted">Số lượng để trống nghĩa là &ldquo;vừa đủ&rdquo; (vd. muối, tiêu).</p>
        <RecipeIngredientsManager
          recipeId={recipe.id}
          initialIngredients={recipe.ingredients}
          onChange={(ingredients) =>
            queryClient.setQueryData<RecipeDetailDto>(queryKey, (current) => (current ? { ...current, ingredients } : current))
          }
        />
      </section>

      <section aria-labelledby="steps-heading" className="mt-8 rounded-lg border border-border bg-surface p-6">
        <h2 id="steps-heading" className="mb-4 text-lg font-semibold text-ink">
          Các bước thực hiện
        </h2>
        <RecipeStepsManager
          recipeId={recipe.id}
          initialSteps={recipe.steps}
          onChange={(steps) =>
            queryClient.setQueryData<RecipeDetailDto>(queryKey, (current) => (current ? { ...current, steps } : current))
          }
        />
      </section>

      <section aria-labelledby="images-heading" className="mt-8 rounded-lg border border-border bg-surface p-6">
        <h2 id="images-heading" className="mb-4 text-lg font-semibold text-ink">
          Ảnh minh họa
        </h2>
        <RecipeImageManager recipeId={recipe.id} initialImages={toImageDtos(recipe)} />
      </section>
    </div>
  );
}
