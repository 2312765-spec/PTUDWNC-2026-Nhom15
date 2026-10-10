import { useQuery } from '@tanstack/react-query';
import { getMe } from '@/lib/auth/api';

export const currentUserQueryKey = ['auth', 'me'] as const;

/** FR-AUTH-006 — hồ sơ user hiện tại; dùng chung cho các trang dashboard. */
export function useCurrentUser() {
  return useQuery({ queryKey: currentUserQueryKey, queryFn: getMe });
}
