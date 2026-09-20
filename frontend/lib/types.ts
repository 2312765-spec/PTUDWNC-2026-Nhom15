/**
 * Kiểu dữ liệu dùng chung giữa 4 người.
 * HỢP ĐỒNG CHUNG — B định nghĩa shape DTO, chốt tuần 1, đóng băng sau đó.
 * Phải khớp với DTO của backend.
 */

/** FR-SRCH-004 — khớp PagedResult<T> của backend. */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** SRS 7.2 — khớp enum RecipeStatus của backend. */
export const RecipeStatus = {
  Draft: 0,
  Published: 1,
  Archived: 2,
} as const;
export type RecipeStatus = (typeof RecipeStatus)[keyof typeof RecipeStatus];

/** SRS 7.2 — khớp enum RecipeDifficulty. */
export const RecipeDifficulty = {
  Easy: 1,
  Medium: 2,
  Hard: 3,
  Expert: 4,
} as const;
export type RecipeDifficulty = (typeof RecipeDifficulty)[keyof typeof RecipeDifficulty];

/** D5 — KHÔNG có fullName hay userName. */
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  bio: string | null;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

// TODO(S3 — B): CategoryDto, CategoryDetailDto
// TODO(S4 — B): RecipeSummaryDto, RecipeDetailDto
// TODO(S6 — C): RecipeStepDto (timerMinutes — D6), RecipeIngredientDto (orderIndex — D7)
// TODO(S8 — D): RecipeImageDto
