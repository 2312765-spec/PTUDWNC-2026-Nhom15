import { apiClient } from '@/lib/api-client';
import type { CategoryDto, PagedResult, RecipeDetailDto, RecipeIngredientDto, RecipeStepDto, RecipeSummaryDto } from '@/lib/types';
import type { toCreateRecipeRequest, toIngredientRequest, toRecipeBasicsRequest, toStepRequest } from '@/lib/recipe-schema';

/**
 * Lời gọi API cho trang tạo/sửa công thức (dashboard, CSR). Bearer do interceptor của apiClient gắn (D74).
 * FR-RCP-003 · FR-RCP-004 (If-Match — D71) · FR-RCP-005 (D77) · FR-RCP-007 (D78) · FR-RCP-009 (D76) ·
 * FR-RCP-010 (D6, D73) · D75 (GET theo id).
 */

/** D81 — trạng thái lọc ở /dashboard/recipes; khớp tham số `status` của GET /recipes. */
export type MyRecipesStatus = 'Draft' | 'Published' | 'Archived';

export const MY_RECIPES_PAGE_SIZE = 10;

export const recipeEditorKeys = {
  recipe: (id: string) => ['dashboard', 'recipe', id] as const,
  /** Tiền tố chung — invalidate cả mọi tab/trang của danh sách sau khi đổi trạng thái hoặc xóa. */
  myRecipesAll: ['dashboard', 'my-recipes'] as const,
  myRecipes: (status: MyRecipesStatus | null, page: number) => ['dashboard', 'my-recipes', status ?? 'all', page] as const,
  categories: ['dashboard', 'recipe-categories'] as const,
};

export async function fetchCategoryOptions(): Promise<CategoryDto[]> {
  const { data } = await apiClient.get<CategoryDto[]>('/categories');
  return data;
}

/** D81 — công thức của người đang đăng nhập (mọi trạng thái), lọc theo `status` nếu có. */
export async function fetchMyRecipes(status: MyRecipesStatus | null, page: number): Promise<PagedResult<RecipeSummaryDto>> {
  const params = status
    ? { mine: true, status, page, pageSize: MY_RECIPES_PAGE_SIZE }
    : { mine: true, page, pageSize: MY_RECIPES_PAGE_SIZE };
  const { data } = await apiClient.get<PagedResult<RecipeSummaryDto>>('/recipes', { params });
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

/** FR-RCP-009/D76 — `orderIndex` vắng = xếp cuối. Trả nguyên liệu vừa tạo. */
export async function addIngredient(
  recipeId: string,
  body: ReturnType<typeof toIngredientRequest> & { orderIndex?: number },
): Promise<RecipeIngredientDto> {
  const { data } = await apiClient.post<RecipeIngredientDto>(`/recipes/${recipeId}/ingredients`, body);
  return data;
}

/** FR-RCP-009/D76 — `orderIndex` có giá trị thì chuyển tới vị trí đó (0-based); trả toàn bộ nguyên liệu theo thứ tự mới. */
export async function updateIngredient(
  recipeId: string,
  ingredientId: string,
  body: ReturnType<typeof toIngredientRequest> & { orderIndex?: number },
): Promise<RecipeIngredientDto[]> {
  const { data } = await apiClient.put<RecipeIngredientDto[]>(`/recipes/${recipeId}/ingredients/${ingredientId}`, body);
  return data;
}

export async function deleteIngredient(recipeId: string, ingredientId: string): Promise<void> {
  await apiClient.delete(`/recipes/${recipeId}/ingredients/${ingredientId}`);
}

/**
 * FR-RCP-005/D77 — đổi trạng thái xuất bản. Không gửi If-Match (tùy chọn ở backend): đổi trạng thái không ghi
 * đè nội dung. Trả RecipeDetailDto với rowVersion mới — trang PHẢI cập nhật cache, nếu không lần lưu thông tin
 * chung kế tiếp sẽ 409.
 */
export async function setRecipePublished(id: string, publish: boolean): Promise<RecipeDetailDto> {
  const { data } = await apiClient.patch<RecipeDetailDto>(`/recipes/${id}/${publish ? 'publish' : 'unpublish'}`);
  return data;
}

/**
 * FR-RCP-006/D79 — lưu trữ (ẩn khỏi trang công khai, không xóa dữ liệu). Không gửi If-Match, giống publish.
 * Trả RecipeDetailDto với rowVersion mới — trang PHẢI cập nhật cache. Thoát khỏi Archived bằng setRecipePublished (D77).
 */
export async function archiveRecipe(id: string): Promise<RecipeDetailDto> {
  const { data } = await apiClient.patch<RecipeDetailDto>(`/recipes/${id}/archive`);
  return data;
}

/** FR-RCP-007/D1/D78 — xóa mềm. */
export async function deleteRecipe(id: string): Promise<void> {
  await apiClient.delete(`/recipes/${id}`);
}

/** NFR-USE-003 — thông báo theo Application Error Code (bảng cuối docs/decisions.md). */
export const RECIPE_ERROR_MESSAGES: Record<string, string> = {
  RECIPE_NOT_FOUND: 'Không tìm thấy công thức (có thể đã bị xóa).',
  RECIPE_FORBIDDEN: 'Bạn không có quyền sửa công thức này.',
  RECIPE_CONCURRENCY_CONFLICT: 'Công thức vừa được thay đổi ở nơi khác. Hãy tải lại bản mới nhất rồi sửa tiếp.',
  RECIPE_STEP_NOT_FOUND: 'Bước này không còn tồn tại. Hãy tải lại trang.',
  RECIPE_INGREDIENT_NOT_FOUND: 'Nguyên liệu này không còn tồn tại. Hãy tải lại trang.',
  RECIPE_PUBLISH_INCOMPLETE: 'Công thức cần ít nhất 1 nguyên liệu và 1 bước thực hiện để xuất bản (công thức đã xuất bản không được xóa hết).',
  VALIDATION_ERROR: 'Dữ liệu chưa hợp lệ, hãy kiểm tra lại.',
  NETWORK_ERROR: 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.',
};
