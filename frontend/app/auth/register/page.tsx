import type { Metadata } from 'next';
import Link from 'next/link';
import { redirect } from 'next/navigation';
import { ChefHat } from 'lucide-react';
import { auth } from '@/auth';
import { RegisterForm } from './RegisterForm';

/**
 * FR-AUTH-001 — trang đăng ký. Chủ sở hữu: A.
 * Server Component làm khung; phần form là client component (cần state/event).
 */
export const metadata: Metadata = {
  title: 'Đăng ký',
  description: 'Tạo tài khoản Culinary Blog để chia sẻ công thức nấu ăn của bạn.',
  robots: { index: false, follow: true },
};

export default async function RegisterPage() {
  // Điều kiện tiên quyết FR-AUTH-001: người dùng chưa đăng nhập.
  if (await auth()) redirect('/dashboard');

  return (
    <div className="flex min-h-[calc(100svh-4rem)] items-center justify-center px-4 py-12">
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
          <h1 className="mt-6 text-2xl font-bold text-ink">Tạo tài khoản mới</h1>
          <p className="mt-2 text-sm text-ink-muted">
            Đăng ký miễn phí để bắt đầu viết và chia sẻ công thức.
          </p>
        </div>

        <div className="rounded-lg border border-border bg-surface p-6 shadow-sm sm:p-8">
          <RegisterForm />
        </div>

        <p className="mt-6 text-center text-sm text-ink-muted">
          Đã có tài khoản?{' '}
          <Link href="/auth/login" className="font-medium text-brand-700 hover:underline">
            Đăng nhập
          </Link>
        </p>
      </div>
    </div>
  );
}
