import type { Metadata } from 'next';
import Link from 'next/link';
import { redirect } from 'next/navigation';
import { ChefHat } from 'lucide-react';
import { auth } from '@/auth';
import { safeCallbackUrl } from '@/lib/auth/callback-url';
import { LoginForm } from './LoginForm';

/**
 * FR-AUTH-002 — trang đăng nhập. Chủ sở hữu: A.
 * Server Component làm khung; phần form là client component (cần state/event).
 */
export const metadata: Metadata = {
  title: 'Đăng nhập',
  description: 'Đăng nhập Culinary Blog để quản lý và chia sẻ công thức nấu ăn của bạn.',
  robots: { index: false, follow: true },
};

interface LoginPageProps {
  /**
   * Auth.js thêm ?callbackUrl= khi chuyển người dùng chưa đăng nhập về đây, và ?error=
   * khi provider Google (`profile()`) ném lỗi — luồng OAuth không có kênh trả ProblemDetails
   * chi tiết như authorize(), chỉ có mã lỗi chung (D9, xem lib/auth/credentials.ts).
   */
  searchParams: Promise<{
    callbackUrl?: string | string[];
    error?: string | string[];
    loggedOut?: string | string[];
  }>;
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const params = await searchParams;
  const callbackUrl = safeCallbackUrl(params.callbackUrl);
  // FR-AUTH-004 — SessionExpiryWatcher đưa về đây khi refresh token hết hạn/bị thu hồi.
  const sessionExpired = params.error === 'SessionExpired';
  const hasGoogleError = params.error !== undefined && !sessionExpired;

  // FR-AUTH-005 — LogoutButton đưa về đây sau khi đăng xuất.
  const loggedOut = params.loggedOut === '1';

  // Đã đăng nhập thì không cần ở lại trang này.
  if (await auth()) redirect(callbackUrl);

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <Link
            href="/"
            className="inline-flex items-center gap-2 text-brand-700 hover:text-brand-600"
            aria-label="Culinary Blog — về trang chủ"
          >
            <ChefHat className="size-8" aria-hidden="true" />
            <span className="text-xl font-bold">Culinary Blog</span>
          </Link>
          <h1 className="mt-6 text-2xl font-bold text-ink">Chào mừng trở lại</h1>
          <p className="mt-2 text-sm text-ink-muted">
            Đăng nhập để tiếp tục viết và quản lý công thức của bạn.
          </p>
        </div>

        <div className="rounded-lg border border-border bg-surface p-6 shadow-sm sm:p-8">
          <LoginForm
            callbackUrl={callbackUrl}
            showGoogleError={hasGoogleError}
            showSessionExpired={sessionExpired}
            showLoggedOut={loggedOut}
          />
        </div>

        <p className="mt-6 text-center text-sm text-ink-muted">
          Chưa có tài khoản?{' '}
          <Link href="/auth/register" className="font-medium text-brand-700 hover:underline">
            Đăng ký
          </Link>
        </p>
      </div>
    </div>
  );
}
