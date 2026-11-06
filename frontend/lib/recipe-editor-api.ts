import { apiClient } from '@/lib/api-client';
import type { CategoryDto, RecipeDetailDto, RecipeStepDto } from '@/lib/types';
import type { toCreateRecipeRequest, toRecipeBasicsRequest, toStepRequest } from '@/lib/recipe-schema';

/**
 * Lời gọi API cho trang tạo/sửa công thức (dashboard, CSR). Bearer do interceptor của apiClient gắn (D74).
 * FR-RCP-003 · FR-RCP-004 (If-Match — D71) · FR-RCP-010 (D6, D73) · D75 (GET theo id).
 */

export const recipeEditorKeys = {
  recipe: (id: string) => ['dashboard', 'recipe', id] as const,
  categories: ['dashboard', 'recipe-categories'] as const,
};

export async function fetchCategoryOptions(): Promise<CategoryDto[]> {
  const { data } = await apiClient.get<CategoryDto[]>('/categories');
  return data;
}

/** D75 — luôn đọc mới từ DB, có rowVersion hiện tại. */
export async function fetchRecipeById(id: string): Promise<RecipeDetailDto> {
  const { data } = await apiClient.get<RecipeDetailDto>(`/recipes/${encodeURIComponent(id)}`);
  return data;
}

export async function createRecipe(body: ReturnType<typeof toCreateRecipeRequest>): Promise<RecipeDetailDto> {
  const { data } = await apiClient.post<RecipeDetailDto>('/recipes', body);
  return data;
}

/** FR-RCP-004/D71 — If-Match là strong ETag (có ngoặc kép) chứa rowVersion. */
export async function updateRecipe(
  id: string,
  rowVersion: string,
  body: ReturnType<typeof toRecipeBasicsRequest>,
): Promise<RecipeDetailDto> {
  const { data } = await apiClient.put<RecipeDetailDto>(`/recipes/${id}`, body, {
    headers: { 'If-Match': `"${rowVersion}"` },
  });
  return data;
}

export async function addStep(recipeId: string, body: ReturnType<typeof toStepRequest>): Promise<RecipeStepDto> {
  const { data } = await apiClient.post<RecipeStepDto>(`/recipes/${recipeId}/steps`, body);
  return data;
}

/** D6/D73 — `stepNumber` có giá trị thì chuyển bước tới vị trí đó; trả toàn bộ bước theo thứ tự mới. */
export async function updateStep(
  recipeId: string,
  stepId: string,
  body: ReturnType<typeof toStepRequest> & { stepNumber?: number },
): Promise<RecipeStepDto[]> {
  const { data } = await apiClient.put<RecipeStepDto[]>(`/recipes/${recipeId}/steps/${stepId}`, body);
  return data;
}

export async function deleteStep(recipeId: string, stepId: string): Promise<void> {
  await apiClient.delete(`/recipes/${recipeId}/steps/${stepId}`);
}

/** NFR-USE-003 — thông báo theo Application Error Code (bảng cuối docs/decisions.md). */
export const RECIPE_ERROR_MESSAGES: Record<string, string> = {
  RECIPE_NOT_FOUND: 'Không tìm thấy công thức (có thể đã bị xóa).',
  RECIPE_FORBIDDEN: 'Bạn không có quyền sửa công thức này.',
  RECIPE_CONCURRENCY_CONFLICT: 'Công thức vừa được thay đổi ở nơi khác. Hãy tải lại bản mới nhất rồi sửa tiếp.',
  RECIPE_STEP_NOT_FOUND: 'Bước này không còn tồn tại. Hãy tải lại trang.',
  RECIPE_PUBLISH_INCOMPLETE: 'Công thức đã xuất bản phải còn ít nhất 1 bước thực hiện.',
  VALIDATION_ERROR: 'Dữ liệu chưa hợp lệ, hãy kiểm tra lại.',
  NETWORK_ERROR: 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.',
};
