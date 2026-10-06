import { ApiError } from '@/lib/categories';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';

/** FR-SRCH-001 / decisions.md: `q` tối thiểu 2 ký tự — dưới ngưỡng thì không gọi API. */
export const SEARCH_MIN_LENGTH = 2;

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
  const res = await fetch(`${API_BASE_URL}/recipes/search?${params}`, { cache: 'no-store' });
  if (!res.ok) {
    const problem = (await res.json().catch(() => null)) as { type?: string } | null;
    throw new ApiError(res.status, problem?.type ?? 'UNKNOWN_ERROR');
  }
  return res.json();
}
