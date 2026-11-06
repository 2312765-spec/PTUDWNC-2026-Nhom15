import { z } from 'zod';

/**
 * FR-RCP-003/004/010 — khớp validator backend (RecipeInputs.cs, D6, D7, D19, D69).
 * Số được giữ dạng chuỗi trong form (ô input trống ≠ 0) và đổi sang số khi gửi.
 * Validate ở client chỉ để phản hồi nhanh — backend vẫn là nguồn quyết định (CONS-008).
 */

const integerText = /^\d+$/;
/** Chấp nhận "0,5" lẫn "0.5" — người Việt quen dấu phẩy thập phân. */
const decimalText = /^\d+([.,]\d{1,3})?$/;

const requiredInt = (label: string, min: number) =>
  z
    .string()
    .trim()
    .refine((v) => integerText.test(v), `${label} phải là số nguyên.`)
    .refine((v) => Number(v) >= min, min === 0 ? `${label} không được âm.` : `${label} phải lớn hơn 0.`)
    .refine((v) => Number(v) <= 100_000, `${label} quá lớn.`);

export const difficultyOptions = [
  { value: '1', label: 'Dễ' },
  { value: '2', label: 'Trung bình' },
  { value: '3', label: 'Khó' },
  { value: '4', label: 'Chuyên gia' },
] as const;

export const recipeBasicsSchema = z.object({
  title: z.string().trim().min(5, 'Tiêu đề phải từ 5 đến 200 ký tự.').max(200, 'Tiêu đề phải từ 5 đến 200 ký tự.'),
  description: z
    .string()
    .trim()
    .min(1, 'Mô tả không được để trống.')
    .max(2000, 'Mô tả không được vượt quá 2000 ký tự.'),
  categoryId: z.string().min(1, 'Hãy chọn danh mục.'),
  prepTime: requiredInt('Thời gian chuẩn bị', 1),
  // D19 — 0 = món không cần nấu (salad, gỏi).
  cookTime: requiredInt('Thời gian nấu', 0),
  servings: requiredInt('Khẩu phần', 1),
  difficulty: z.enum(['1', '2', '3', '4'], { errorMap: () => ({ message: 'Hãy chọn độ khó.' }) }),
  instructions: z.string(),
});

export const ingredientSchema = z.object({
  name: z.string().trim().min(1, 'Nhập tên nguyên liệu.').max(200, 'Tên nguyên liệu tối đa 200 ký tự.'),
  // D7 — để trống cho "muối vừa đủ"; có giá trị thì phải > 0.
  quantity: z
    .string()
    .trim()
    .refine((v) => v === '' || decimalText.test(v), 'Số lượng không hợp lệ (vd. 2 hoặc 0,5).')
    .refine((v) => v === '' || Number(v.replace(',', '.')) > 0, 'Số lượng phải lớn hơn 0.'),
  unit: z.string().trim().max(50, 'Đơn vị tối đa 50 ký tự.'),
  notes: z.string().trim().max(500, 'Ghi chú tối đa 500 ký tự.'),
});

export const stepSchema = z.object({
  title: z.string().trim().min(1, 'Nhập tiêu đề bước.').max(200, 'Tiêu đề bước tối đa 200 ký tự.'),
  description: z
    .string()
    .trim()
    .min(1, 'Nhập mô tả bước.')
    .max(2000, 'Mô tả bước tối đa 2000 ký tự.'),
  timerMinutes: z
    .string()
    .trim()
    .refine((v) => v === '' || integerText.test(v), 'Thời gian phải là số phút (số nguyên, không âm).'),
});

/** Tối đa 100 step / 100 ingredient mỗi request (D69). */
export const MAX_CHILDREN = 100;

export const createRecipeSchema = recipeBasicsSchema.extend({
  ingredients: z.array(ingredientSchema).max(MAX_CHILDREN, `Tối đa ${MAX_CHILDREN} nguyên liệu.`),
  steps: z.array(stepSchema).max(MAX_CHILDREN, `Tối đa ${MAX_CHILDREN} bước.`),
});

export type RecipeBasicsValues = z.infer<typeof recipeBasicsSchema>;
export type IngredientValues = z.infer<typeof ingredientSchema>;
export type StepValues = z.infer<typeof stepSchema>;
export type CreateRecipeValues = z.infer<typeof createRecipeSchema>;

export const emptyIngredient: IngredientValues = { name: '', quantity: '', unit: '', notes: '' };
export const emptyStep: StepValues = { title: '', description: '', timerMinutes: '' };

export const emptyRecipeBasics: RecipeBasicsValues = {
  title: '',
  description: '',
  categoryId: '',
  prepTime: '',
  cookTime: '0',
  servings: '4',
  difficulty: '1',
  instructions: '',
};

const blankToNull = (v: string) => (v.trim() === '' ? null : v.trim());

/** D69 — body của PUT /recipes/{id} (cũng là phần chung của POST). `nutrition` không gửi = giữ nguyên. */
export function toRecipeBasicsRequest(values: RecipeBasicsValues) {
  return {
    title: values.title.trim(),
    description: values.description.trim(),
    categoryId: values.categoryId,
    prepTime: Number(values.prepTime),
    cookTime: Number(values.cookTime),
    servings: Number(values.servings),
    difficulty: Number(values.difficulty),
    instructions: blankToNull(values.instructions),
  };
}

export function toStepRequest(values: StepValues) {
  return {
    title: values.title.trim(),
    description: values.description.trim(),
    timerMinutes: values.timerMinutes.trim() === '' ? null : Number(values.timerMinutes),
  };
}

/** FR-RCP-003 — body của POST /recipes: thông tin chung + steps + ingredients theo đúng thứ tự trên form. */
export function toCreateRecipeRequest(values: CreateRecipeValues) {
  return {
    ...toRecipeBasicsRequest(values),
    ingredients: values.ingredients.map((i) => ({
      name: i.name.trim(),
      quantity: i.quantity.trim() === '' ? null : Number(i.quantity.trim().replace(',', '.')),
      unit: blankToNull(i.unit),
      notes: blankToNull(i.notes),
    })),
    steps: values.steps.map(toStepRequest),
  };
}

/**
 * NFR-USE-003 — key lỗi của backend theo PropertyName FluentValidation ("Title", "Steps[0].Title",
 * "Ingredients[1].Quantity") → đường dẫn field của React Hook Form ("title", "steps.0.title").
 */
export function toFormPath(key: string): string {
  return key
    .split('.')
    .map((segment) => segment.replace(/\[(\d+)\]/g, '.$1'))
    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
    .join('.');
}
