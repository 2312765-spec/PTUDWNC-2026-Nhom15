import axios from 'axios';

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

// Giữ đường import cũ cho code đã dùng; code client không cần axios nên import thẳng từ problem-details.
export { toProblemDetails, type ProblemDetails } from '@/lib/problem-details';
