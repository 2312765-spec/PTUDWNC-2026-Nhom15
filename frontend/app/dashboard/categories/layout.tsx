'use client';

import { ShieldAlert } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { useEffect, type ReactNode } from 'react';
import { Skeleton } from '@/components/ui';

const CATEGORIES_PATH = '/dashboard/categories';

/**
 * Chặn quyền cho /dashboard/categories — chỉ Admin (docs/permissions.md, app/dashboard/OWNER.md).
 * FR-CAT-001/003/004/005. Role lấy từ phiên Auth.js (session.profile.roles), KHÔNG đọc
 * localStorage: token nằm trong cookie httpOnly, JS trình duyệt không thấy.
 *
 * Đây chỉ là UX — backend vẫn chặn bằng AdminPolicy (NFR-SEC-006).
 */
export default function AdminCategoriesLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const { data: session, status } = useSession();

  useEffect(() => {
    if (status === 'unauthenticated') {
      router.replace(`/auth/login?callbackUrl=${encodeURIComponent(CATEGORIES_PATH)}`);
    }
  }, [status, router]);

  // Đang tải phiên, hoặc đang chuyển sang trang đăng nhập.
  if (status !== 'authenticated') {
    return (
      <div
        aria-busy="true"
        aria-label="Đang kiểm tra quyền truy cập"
        className="mx-auto flex max-w-5xl flex-col gap-3 px-4 py-10"
      >
        <Skeleton className="h-8 w-56" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    );
  }

  // Mặc định chặn: thiếu profile hoặc không có role Admin đều là 403.
  if (!session?.profile?.roles.includes('Admin')) {
    return (
      <div role="alert" className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
        <ShieldAlert className="size-10 text-red-600" aria-hidden="true" />
        <h1 className="mt-3 text-xl font-bold text-ink">Truy cập bị từ chối (403)</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Trang quản trị danh mục chỉ dành cho tài khoản có quyền <strong>Quản trị viên (Admin)</strong>.
        </p>
        <Link
          href="/"
          className="mt-6 inline-flex h-10 items-center rounded-md bg-brand-600 px-4 text-sm font-medium text-white hover:bg-brand-700"
        >
          Trở về Trang chủ
        </Link>
      </div>
    );
  }

  return <>{children}</>;
}
