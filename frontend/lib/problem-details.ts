/**
 * RFC 7807 Problem Details — CONS-005.
 *
 * Tách khỏi `api-client.ts` để KHÔNG import axios (NFR-PERF-005/D64): form đăng nhập/đăng ký chỉ
 * cần đọc lỗi, và route của chúng bị `<Link>` trên header prefetch ở mọi trang public — import
 * `api-client` sẽ kéo ~20KB axios vào JS của cả site.
 */
export interface ProblemDetails {
  /** Application Error Code, ví dụ "VALIDATION_ERROR" — xem docs/decisions.md. */
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  /** Chỉ có khi lỗi validation: { fieldName: ["thông báo"] }. */
  errors?: Record<string, string[]>;
  correlationId?: string;
}

/** Thay `instanceof AxiosError`: mọi lỗi axios mang cờ `isAxiosError` (chính `axios.isAxiosError` cũng kiểm cờ này). */
function isAxiosError(error: unknown): error is { response?: { data?: unknown } } {
  return typeof error === 'object' && error !== null && (error as { isAxiosError?: unknown }).isAxiosError === true;
}

/**
 * NFR-USE-003 — xử lý lỗi theo error CODE, không theo chuỗi message
 * (message có thể đổi theo locale).
 */
export function toProblemDetails(error: unknown): ProblemDetails {
  if (isAxiosError(error) && error.response?.data) {
    return error.response.data as ProblemDetails;
  }

  return {
    type: 'NETWORK_ERROR',
    title: 'Không kết nối được máy chủ',
    status: 0,
    detail: 'Vui lòng kiểm tra kết nối mạng và thử lại.',
  };
}
