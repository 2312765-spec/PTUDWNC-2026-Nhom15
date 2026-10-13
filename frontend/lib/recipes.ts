import { ApiError } from '@/lib/categories';
import type { PagedResult, RecipeDetailDto, RecipeSummaryDto } from '@/lib/types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';

/** FR-SRCH-001 / decisions.md: `q` tối thiểu 2 ký tự — dưới ngưỡng thì không gọi API. */
export const SEARCH_MIN_LENGTH = 2;

/** FR-SRCH-002 (D14) — giá trị gửi lên khớp tên enum RecipeDifficulty của backend. */
export const DIFFICULTY_OPTIONS = [
  { value: 'Easy', label: 'Dễ' },
  { value: 'Medium', label: 'Trung bình' },
  { value: 'Hard', label: 'Khó' },
  { value: 'Expert', label: 'Rất khó' },
] as const;

/** FR-SRCH-003 — danh sách sort backend chấp nhận; mặc định `-createdAt`. */
export const SORT_OPTIONS = [
  { value: '-createdAt', label: 'Mới nhất' },
  { value: 'createdAt', label: 'Cũ nhất' },
  { value: 'title', label: 'Tên A → Z' },
  { value: '-title', label: 'Tên Z → A' },
  { value: 'cookTime', label: 'Nấu nhanh nhất' },
  { value: '-cookTime', label: 'Nấu lâu nhất' },
] as const;

export const DEFAULT_SORT = '-createdAt';

/** FR-SRCH-002/003/004 — tham số của `GET /recipes`; trường rỗng thì không gửi. */
export interface RecipeListFilters {
  categoryId?: string;
  difficulty?: string;
  maxCookTime?: string;
  minServings?: string;
  sort?: string;
  page?: number;
}

/** Chuyển bộ lọc thành query string, bỏ trường rỗng và giá trị mặc định (URL gọn, cache key ổn định). */
export function toRecipeListQuery(filters: RecipeListFilters, pageSize?: number): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.categoryId) params.set('categoryId', filters.categoryId);
  if (filters.difficulty) params.set('difficulty', filters.difficulty);
  if (filters.maxCookTime) params.set('maxCookTime', filters.maxCookTime);
  if (filters.minServings) params.set('minServings', filters.minServings);
  if (filters.sort && filters.sort !== DEFAULT_SORT) params.set('sort', filters.sort);
  if (filters.page && filters.page > 1) params.set('page', String(filters.page));
  if (pageSize) params.set('pageSize', String(pageSize));
  return params;
}

/** NFR-USE-003 — lỗi RFC 7807 → {@link ApiError} mang Application Error Code (`type`). */
async function toApiError(res: Response): Promise<ApiError> {
  const problem = (await res.json().catch(() => null)) as { type?: string } | null;
  return new ApiError(res.status, problem?.type ?? 'UNKNOWN_ERROR');
}

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw await toApiError(res);
  return res.json();
}

/**
 * FR-SRCH-001 — SRS 5.1: `/search` SSR → `no-store` (backend tự cache Redis 1 phút, D8).
 * Lỗi ném {@link ApiError} kèm Application Error Code để trang xử lý theo code (NFR-USE-003).
 */
export async function searchRecipes(
  q: string,
  page = 1,
  pageSize = 12,
): Promise<PagedResult<RecipeSummaryDto>> {
  const params = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) });
  return getJson(`${API_BASE_URL}/recipes/search?${params}`);
}

/**
 * FR-RCP-001 + FR-SRCH-002/003/004 — SRS 5.1: `/recipes` SSR → `no-store` (backend cache Redis
 * 15 phút, D8). Gọi từ server không kèm token nên luôn là góc nhìn Guest (chỉ Published);
 * Draft/Archived của Author xem ở `/dashboard`.
 */
export async function getRecipes(
  filters: RecipeListFilters,
  pageSize = 12,
): Promise<PagedResult<RecipeSummaryDto>> {
  return getJson(`${API_BASE_URL}/recipes?${toRecipeListQuery(filters, pageSize)}`);
}

/**
 * FR-RCP-001 — công thức mới nhất cho trang chủ. SRS 5.1: `/` ISR 3600 → dùng `revalidate`,
 * KHÔNG `no-store` (no-store biến cả trang thành dynamic). Sort mặc định của backend là
 * `-createdAt` nên không gửi.
 */
export async function getLatestRecipes(pageSize: number): Promise<PagedResult<RecipeSummaryDto>> {
  const res = await fetch(`${API_BASE_URL}/recipes?${toRecipeListQuery({}, pageSize)}`, {
    next: { revalidate: 3600 },
  });
  if (!res.ok) throw await toApiError(res);
  return res.json();
}

/**
 * FR-RCP-002 — SRS 5.1: `/recipes/[slug]` ISR 300, tag `recipes` + `recipe:{slug}` (D8).
 *
 * Chỉ 404/403 → `null` (trang gọi notFound(); 403 = Draft của người khác, trả 404 để không lộ là
 * nó tồn tại). Lỗi khác (5xx, mất kết nối) phải NÉM ra: trả `null` sẽ biến sự cố thành 404 giả,
 * ISR lưu trang 404 đè lên bản tốt và Google gỡ trang khỏi index (NFR-USE-003, NFR-SEO).
 */
export async function getRecipeBySlug(slug: string): Promise<RecipeDetailDto | null> {
  const res = await fetch(`${API_BASE_URL}/recipes/${encodeURIComponent(slug)}`, {
    next: { revalidate: 300, tags: ['recipes', `recipe:${slug}`] },
  });
  if (res.status === 404 || res.status === 403) return null;
  if (!res.ok) throw await toApiError(res);
  return res.json();
}
