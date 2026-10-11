'use client';

import { signIn } from 'next-auth/react';
import { useState } from 'react';
import { Button } from '@/components/ui';

/** Logo Google chính thức (đa sắc) — lucide-react không có icon thương hiệu bên thứ ba. */
function GoogleLogo() {
  return (
    <svg viewBox="0 0 24 24" className="size-4 shrink-0" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
      />
      <path
        fill="#34A853"
        d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.67-2.23 1.06-3.71 1.06-2.86 0-5.28-1.93-6.15-4.53H2.18v2.85C3.99 20.53 7.7 23 12 23z"
      />
      <path
        fill="#FBBC05"
        d="M5.85 14.1c-.22-.67-.35-1.37-.35-2.1s.13-1.43.35-2.1V7.05H2.18A11 11 0 0 0 1 12c0 1.77.43 3.45 1.18 4.95z"
      />
      <path
        fill="#EA4335"
        d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.05l3.67 2.85c.87-2.6 3.29-4.52 6.15-4.52z"
      />
    </svg>
  );
}

/**
 * FR-AUTH-003 — nút "Tiếp tục với Google", dùng chung cho `/auth/login` và `/auth/register`
 * (một luồng OAuth xử lý cả đăng nhập lẫn tự tạo tài khoản, D9). Nhấn nút chuyển hẳn trang
 * sang Google nên `loading` chỉ cần bật một chiều — không có async operation nào ở đây.
 */
export function GoogleSignInButton({ callbackUrl = '/dashboard' }: { callbackUrl?: string }) {
  const [loading, setLoading] = useState(false);

  return (
    <Button
      type="button"
      variant="outline"
      size="lg"
      loading={loading}
      className="w-full"
      onClick={() => {
        setLoading(true);
        void signIn('google', { callbackUrl });
      }}
    >
      {!loading && <GoogleLogo />}
      {loading ? 'Đang chuyển tới Google…' : 'Tiếp tục với Google'}
    </Button>
  );
}
