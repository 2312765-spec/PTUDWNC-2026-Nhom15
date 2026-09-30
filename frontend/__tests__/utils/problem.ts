import { AxiosError, AxiosHeaders } from 'axios';
import type { ProblemDetails } from '@/lib/api-client';

/** AxiosError thật mang body RFC 7807 — để toProblemDetails() chạy đúng đường code thật. */
export function problemError(problem: ProblemDetails): AxiosError {
  const headers = new AxiosHeaders();
  return new AxiosError(problem.title, 'ERR_BAD_REQUEST', { headers }, null, {
    status: problem.status,
    statusText: problem.title,
    headers: {},
    config: { headers },
    data: problem,
  });
}

/** Lỗi mạng: không có response. */
export function networkError(): AxiosError {
  return new AxiosError('Network Error', 'ERR_NETWORK');
}
