'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm, type FieldPath } from 'react-hook-form';
import { Button, useToast } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { fetchCategoryOptions, RECIPE_ERROR_MESSAGES, recipeEditorKeys, updateRecipe } from '@/lib/recipe-editor-api';
import { recipeBasicsSchema, toFormPath, toRecipeBasicsRequest, type RecipeBasicsValues } from '@/lib/recipe-schema';
import type { RecipeDetailDto } from '@/lib/types';
import { RecipeBasicsFields } from './RecipeBasicsFields';

export function toBasicsValues(recipe: RecipeDetailDto): RecipeBasicsValues {
  return {
    title: recipe.title,
    description: recipe.description,
    categoryId: recipe.category.id,
    prepTime: String(recipe.prepTime),
    cookTime: String(recipe.cookTime),
    servings: String(recipe.servings),
    difficulty: String(recipe.difficulty) as RecipeBasicsValues['difficulty'],
    instructions: recipe.instructions ?? '',
  };
}

export interface RecipeBasicsFormProps {
  recipe: RecipeDetailDto;
  /** Lưu thành công → DTO mới (có rowVersion mới — D71). */
  onSaved: (recipe: RecipeDetailDto) => void;
  /** 409 → người dùng chọn tải lại bản mới nhất. */
  onReload: () => Promise<RecipeDetailDto | undefined>;
}

/**
 * FR-RCP-004 — sửa thông tin chung. PUT thay toàn bộ (D69), header If-Match = rowVersion đang xem (D71).
 * 409 RECIPE_CONCURRENCY_CONFLICT: không ghi đè, báo người dùng và cho tải bản mới nhất.
 * Slug giữ nguyên dù đổi tiêu đề (D13).
 */
export function RecipeBasicsForm({ recipe, onSaved, onReload }: RecipeBasicsFormProps) {
  const toast = useToast();
  const [formError, setFormError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [reloading, setReloading] = useState(false);
  const categories = useQuery({ queryKey: recipeEditorKeys.categories, queryFn: fetchCategoryOptions });

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm<RecipeBasicsValues>({
    resolver: zodResolver(recipeBasicsSchema),
    defaultValues: toBasicsValues(recipe),
  });

  const mutation = useMutation({
    onMutate: () => {
      setFormError(null);
      setConflict(false);
    },
    mutationFn: (values: RecipeBasicsValues) => updateRecipe(recipe.id, recipe.rowVersion, toRecipeBasicsRequest(values)),
    onSuccess: (updated) => {
      reset(toBasicsValues(updated));
      onSaved(updated);
      toast.show({ variant: 'success', title: 'Đã lưu thay đổi', description: `"${updated.title}" đã được cập nhật.` });
    },
    onError: (err) => {
      const problem = toProblemDetails(err);
      if (problem.type === 'RECIPE_CONCURRENCY_CONFLICT') {
        setConflict(true);
        return;
      }
      let mapped = false;
      for (const [key, messages] of Object.entries(problem.errors ?? {})) {
        const path = toFormPath(key);
        if (messages[0] && path in toBasicsValues(recipe)) {
          setError(path as FieldPath<RecipeBasicsValues>, { message: messages[0] });
          mapped = true;
        }
      }
      if (!mapped) setFormError(RECIPE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? problem.title);
    },
  });

  async function reloadLatest() {
    setReloading(true);
    try {
      const latest = await onReload();
      if (latest) {
        reset(toBasicsValues(latest));
        setConflict(false);
        toast.show({ variant: 'info', title: 'Đã tải bản mới nhất', description: 'Thay đổi chưa lưu của bạn đã bị bỏ.' });
      }
    } finally {
      setReloading(false);
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="flex flex-col gap-4">
      {conflict && (
        <div role="alert" className="flex flex-col gap-3 rounded-md border border-warning-line bg-warning-subtle p-3 text-sm text-warning">
          <p>{RECIPE_ERROR_MESSAGES.RECIPE_CONCURRENCY_CONFLICT}</p>
          <Button type="button" variant="outline" size="sm" className="self-start" loading={reloading} onClick={() => void reloadLatest()}>
            Tải lại bản mới nhất
          </Button>
        </div>
      )}
      {formError && (
        <p role="alert" className="rounded-md border border-danger-line bg-danger-subtle p-3 text-sm text-danger">
          {formError}
        </p>
      )}

      <RecipeBasicsFields
        register={register}
        errors={errors}
        categories={categories.data}
        categoriesLoading={categories.isPending}
      />

      <div className="flex justify-end gap-2 border-t border-border pt-4">
        <Button type="button" variant="outline" disabled={!isDirty || mutation.isPending} onClick={() => reset(toBasicsValues(recipe))}>
          Hoàn tác
        </Button>
        <Button type="submit" loading={mutation.isPending} disabled={!isDirty}>
          Lưu thông tin chung
        </Button>
      </div>
    </form>
  );
}
