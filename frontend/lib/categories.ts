import type { CategoryDetailResponseDto, CategoryDto } from '@/lib/types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';

/** Lỗi API kèm Application Error Code (RFC 7807 `type`) — NFR-USE-003: xử lý theo code, không theo message. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
  ) {
    super(`API ${status} ${code}`);
    this.name = 'ApiError';
  }
}

async function toApiError(res: Response): Promise<ApiError> {
  const problem = (await res.json().catch(() => null)) as { type?: string } | null;
  return new ApiError(res.status, problem?.type ?? 'UNKNOWN_ERROR');
}

/**
 * FR-CAT-001 — SRS 5.1: `/categories` ISR 3600. Backend tự cache Redis 30 phút (D8).
 */
export async function getCategories(): Promise<CategoryDto[]> {
  const res = await fetch(`${API_BASE_URL}/categories`, { next: { revalidate: 3600 } });
  if (!res.ok) throw await toApiError(res);
  return res.json();
}

/**
 * FR-CAT-002 — SRS 5.1: `/categories/[slug]` ISR 600.
 * Trả `null` khi slug không tồn tại (404 CATEGORY_NOT_FOUND) để trang gọi `notFound()`.
 */
export async function getCategoryBySlug(
  slug: string,
  page = 1,
  pageSize = 12,
): Promise<CategoryDetailResponseDto | null> {
  const res = await fetch(
    `${API_BASE_URL}/categories/${encodeURIComponent(slug)}?page=${page}&pageSize=${pageSize}`,
    { next: { revalidate: 600 } },
  );
  if (res.status === 404) return null;
  if (!res.ok) throw await toApiError(res);
  return res.json();
}
