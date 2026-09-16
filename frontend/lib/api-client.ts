import axios, { AxiosError } from 'axios';

/**
 * HỢP ĐỒNG CHUNG — chủ sở hữu: C (Sprint 0). Cả 4 người dùng.
 *
 * TODO(S2 — A): thêm interceptor tự refresh khi nhận 401 (FR-AUTH-004),
 * gắn Authorization: Bearer từ session Auth.js.
 */
export const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
});

/** RFC 7807 Problem Details — CONS-005. */
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

/**
 * NFR-USE-003 — xử lý lỗi theo error CODE, không theo chuỗi message
 * (message có thể đổi theo locale).
 */
export function toProblemDetails(error: unknown): ProblemDetails {
  if (error instanceof AxiosError && error.response?.data) {
    return error.response.data as ProblemDetails;
  }

  return {
    type: 'NETWORK_ERROR',
    title: 'Không kết nối được máy chủ',
    status: 0,
    detail: 'Vui lòng kiểm tra kết nối mạng và thử lại.',
  };
}
