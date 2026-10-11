import { ApiError } from '@/lib/categories';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';

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

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) {
    const problem = (await res.json().catch(() => null)) as { type?: string } | null;
    throw new ApiError(res.status, problem?.type ?? 'UNKNOWN_ERROR');
  }
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
