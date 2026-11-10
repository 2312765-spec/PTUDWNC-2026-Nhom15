'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Check } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { useForm, type FieldPath } from 'react-hook-form';
import { Button, useToast } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { createRecipe, fetchCategoryOptions, RECIPE_ERROR_MESSAGES, recipeEditorKeys } from '@/lib/recipe-editor-api';
import {
  createRecipeSchema,
  difficultyOptions,
  emptyIngredient,
  emptyRecipeBasics,
  emptyStep,
  toCreateRecipeRequest,
  toFormPath,
  type CreateRecipeValues,
} from '@/lib/recipe-schema';
import { cn } from '@/lib/utils';
import { IngredientListEditor } from './IngredientListEditor';
import { RecipeBasicsFields } from './RecipeBasicsFields';
import { StepListEditor } from './StepListEditor';

const WIZARD_STEPS = [
  { key: 'basics', label: 'Thông tin chung' },
  { key: 'ingredients', label: 'Nguyên liệu' },
  { key: 'steps', label: 'Các bước' },
  { key: 'review', label: 'Xem lại' },
] as const;

const BASIC_FIELDS: FieldPath<CreateRecipeValues>[] = [
  'title',
  'description',
  'categoryId',
  'prepTime',
  'cookTime',
  'servings',
  'difficulty',
  'instructions',
];

/** Trường nào thuộc bước nào của wizard — để nhảy về đúng bước khi server báo lỗi. */
function wizardStepOf(path: string): number {
  if (path.startsWith('ingredients')) return 1;
  if (path.startsWith('steps')) return 2;
  return 0;
}

/**
 * FR-RCP-003 — wizard tạo công thức (SRS 5.1: /dashboard/* CSR). Một form React Hook Form duy nhất,
 * mỗi bước chỉ validate phần của nó; bước cuối gửi một POST /recipes kèm steps + ingredients (D69).
 * Tạo xong → Draft, chuyển sang trang sửa để thêm ảnh / chỉnh tiếp (FR-RCP-004/008/010).
 * NFR-USE-003: lỗi server gắn đúng ô; NFR-USE-004: nút loading + toast.
 */
export function RecipeWizard() {
  const router = useRouter();
  const toast = useToast();
  const [current, setCurrent] = useState(0);
  const [formError, setFormError] = useState<string | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const firstRender = useRef(true);

  const categories = useQuery({ queryKey: recipeEditorKeys.categories, queryFn: fetchCategoryOptions });

  const {
    register,
    control,
    handleSubmit,
    trigger,
    getValues,
    setError,
    formState: { errors },
  } = useForm<CreateRecipeValues>({
    resolver: zodResolver(createRecipeSchema),
    defaultValues: { ...emptyRecipeBasics, ingredients: [{ ...emptyIngredient }], steps: [{ ...emptyStep }] },
    mode: 'onTouched',
  });

  // NFR-USE-002: đổi bước thì đưa focus về tiêu đề bước mới cho người dùng bàn phím / trình đọc màn hình.
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    headingRef.current?.focus();
  }, [current]);

  const mutation = useMutation({
    onMutate: () => setFormError(null),
    mutationFn: (values: CreateRecipeValues) => createRecipe(toCreateRecipeRequest(values)),
    onSuccess: (recipe) => {
      toast.show({
        variant: 'success',
        title: 'Đã tạo công thức',
        description: `"${recipe.title}" đã được lưu dưới dạng nháp.`,
      });
      router.push(`/dashboard/recipes/${recipe.id}/edit`);
    },
    onError: (err) => {
      const problem = toProblemDetails(err);
      let firstStep: number | null = null;
      for (const [key, messages] of Object.entries(problem.errors ?? {})) {
        const path = toFormPath(key) as FieldPath<CreateRecipeValues>;
        if (messages[0]) {
          setError(path, { message: messages[0] });
          firstStep = Math.min(firstStep ?? 3, wizardStepOf(path));
        }
      }
      if (firstStep !== null) {
        setCurrent(firstStep);
        return;
      }
      setFormError(RECIPE_ERROR_MESSAGES[problem.type] ?? problem.detail ?? problem.title);
    },
  });

  async function next() {
    const valid =
      current === 0
        ? await trigger(BASIC_FIELDS)
        : current === 1
          ? await trigger('ingredients')
          : current === 2
            ? await trigger('steps')
            : true;
    if (valid) setCurrent((c) => Math.min(c + 1, WIZARD_STEPS.length - 1));
  }

  const values = current === 3 ? getValues() : null;
  const categoryName = values && categories.data?.find((c) => c.id === values.categoryId)?.name;
  const difficultyLabel = values && difficultyOptions.find((d) => d.value === values.difficulty)?.label;

  return (
    <form
      noValidate
      onSubmit={(event) => {
        // Enter trong một ô ở bước 1–3 chỉ chuyển bước, không gửi sớm.
        if (current < WIZARD_STEPS.length - 1) {
          event.preventDefault();
          void next();
          return;
        }
        void handleSubmit((v) => mutation.mutate(v))(event);
      }}
      className="flex flex-col gap-6"
      aria-describedby={formError ? 'wizard-error' : undefined}
    >
      <nav aria-label="Các bước tạo công thức">
        <ol className="flex flex-wrap gap-2">
          {WIZARD_STEPS.map((step, index) => (
            <li
              key={step.key}
              aria-current={index === current ? 'step' : undefined}
              className={cn(
                'flex items-center gap-2 rounded-full border px-3 py-1 text-sm',
                index === current
                  ? 'border-brand-600 bg-brand-50 font-semibold text-brand-700'
                  : index < current
                    ? 'border-border text-ink'
                    : 'border-border text-ink-muted',
              )}
            >
              <span
                aria-hidden="true"
                className={cn(
                  'flex size-5 items-center justify-center rounded-full text-xs',
                  index < current ? 'bg-brand-600 text-on-brand' : 'bg-surface-subtle',
                )}
              >
                {index < current ? <Check className="size-3" /> : index + 1}
              </span>
              {step.label}
              {index < current && <span className="sr-only">(đã xong)</span>}
            </li>
          ))}
        </ol>
      </nav>

      {formError && (
        <p id="wizard-error" role="alert" className="rounded-md border border-danger-line bg-danger-subtle p-3 text-sm text-danger">
          {formError}
        </p>
      )}

      <section aria-labelledby="wizard-step-heading" className="rounded-lg border border-border bg-surface p-6">
        <h2 id="wizard-step-heading" ref={headingRef} tabIndex={-1} className="mb-4 text-lg font-semibold text-ink outline-none">
          Bước {current + 1}/{WIZARD_STEPS.length}: {WIZARD_STEPS[current].label}
        </h2>

        {/* Giữ cả 3 bước nhập trong DOM (ẩn bằng hidden) để React Hook Form không mất giá trị khi chuyển bước. */}
        <div hidden={current !== 0}>
          <RecipeBasicsFields
            register={register}
            errors={errors}
            categories={categories.data}
            categoriesLoading={categories.isPending}
          />
          {categories.isError && (
            <p role="alert" className="mt-2 text-sm text-danger">
              Không tải được danh mục.{' '}
              <button type="button" className="underline" onClick={() => categories.refetch()}>
                Thử lại
              </button>
            </p>
          )}
        </div>
        <div hidden={current !== 1}>
          <IngredientListEditor control={control} register={register} errors={errors} />
        </div>
        <div hidden={current !== 2}>
          <StepListEditor control={control} register={register} errors={errors} />
        </div>

        {values && (
          <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-[max-content_1fr]">
            <dt className="font-medium text-ink-muted">Tiêu đề</dt>
            <dd className="text-ink">{values.title}</dd>
            <dt className="font-medium text-ink-muted">Danh mục</dt>
            <dd className="text-ink">{categoryName ?? '—'}</dd>
            <dt className="font-medium text-ink-muted">Thời gian</dt>
            <dd className="text-ink">
              Chuẩn bị {values.prepTime} phút · Nấu {values.cookTime} phút · {values.servings} khẩu phần
            </dd>
            <dt className="font-medium text-ink-muted">Độ khó</dt>
            <dd className="text-ink">{difficultyLabel}</dd>
            <dt className="font-medium text-ink-muted">Nguyên liệu</dt>
            <dd className="text-ink">{values.ingredients.length} nguyên liệu</dd>
            <dt className="font-medium text-ink-muted">Các bước</dt>
            <dd className="text-ink">
              {values.steps.length > 0 ? (
                <ol className="list-decimal pl-5">
                  {values.steps.map((s, i) => (
                    <li key={i}>{s.title}</li>
                  ))}
                </ol>
              ) : (
                'Chưa có bước nào'
              )}
            </dd>
          </dl>
        )}
        {values && (
          <p className="mt-4 text-sm text-ink-muted">
            Công thức được lưu ở trạng thái <strong>Nháp</strong> — chưa ai khác thấy được. Sau khi tạo, bạn có thể
            thêm ảnh và chỉnh sửa tiếp.
          </p>
        )}
      </section>

      <div className="flex justify-between gap-2">
        <Button type="button" variant="outline" disabled={current === 0} onClick={() => setCurrent((c) => c - 1)}>
          Quay lại
        </Button>
        {current < WIZARD_STEPS.length - 1 ? (
          // key riêng: không để React tái dùng cùng phần tử <button> rồi đổi type sang "submit" ngay trong
          // cú click "Tiếp tục" ở bước 3 — trình duyệt sẽ gửi form luôn (tạo trùng công thức).
          <Button key="next" type="button" onClick={() => void next()}>
            Tiếp tục
          </Button>
        ) : (
          <Button key="submit" type="submit" loading={mutation.isPending}>
            Tạo công thức
          </Button>
        )}
      </div>
    </form>
  );
}
