'use client';

import { LogOut } from 'lucide-react';
import { signOut } from 'next-auth/react';
import { useState } from 'react';
import { Button, type ButtonProps } from '@/components/ui';

/**
 * FR-AUTH-005 — nút đăng xuất. signOut kích hoạt `events.signOut` ở server (auth.ts) để thu hồi
 * refresh token, rồi chuyển về trang đăng nhập; trang đó hiện toast "Đã đăng xuất" (NFR-USE-004)
 * nhờ ?loggedOut=1 — redirect làm mất state toast nên không toast tại đây.
 */
export function LogoutButton({
  variant = 'outline',
  size = 'sm',
  ...props
}: Omit<ButtonProps, 'onClick' | 'loading' | 'children' | 'type'>) {
  const [pending, setPending] = useState(false);

  const handleClick = async () => {
    setPending(true);
    try {
      await signOut({ redirectTo: '/auth/login?loggedOut=1' });
    } finally {
      // Điều hướng thường làm trang bị thay thế; reset để không kẹt nếu signOut ném lỗi.
      setPending(false);
    }
  };

  return (
    <Button
      type="button"
      variant={variant}
      size={size}
      loading={pending}
      onClick={handleClick}
      aria-label="Đăng xuất"
      {...props}
    >
      <LogOut className="size-4" aria-hidden="true" />
      {pending ? 'Đang đăng xuất…' : 'Đăng xuất'}
    </Button>
  );
}
