'use client';

import { ShieldAlert } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { useEffect, type ReactNode } from 'react';
import { Skeleton } from '@/components/ui';

/**
 * Chặn quyền cho /dashboard/recipes/* — Author hoặc Admin (docs/permissions.md mục 4, app/dashboard/OWNER.md).
 * Quyền sở hữu từng công thức do backend quyết định (403 RECIPE_FORBIDDEN) — trang sửa tự hiển thị lỗi đó.
 * Chỉ là UX: backend vẫn chặn bằng AuthorPolicy + RecipeAuthorizationHandler (NFR-SEC-006, D70).
 */
export default function DashboardRecipesLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { data: session, status } = useSession();

  useEffect(() => {
    if (status === 'unauthenticated') {
      router.replace(`/auth/login?callbackUrl=${encodeURIComponent(pathname)}`);
    }
  }, [status, router, pathname]);

  if (status !== 'authenticated') {
    return (
      <div aria-busy="true" aria-label="Đang kiểm tra quyền truy cập" className="mx-auto flex max-w-3xl flex-col gap-3 px-4 py-10">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  const roles = session?.profile?.roles ?? [];
  if (!roles.includes('Author') && !roles.includes('Admin')) {
    return (
      <div role="alert" className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
        <ShieldAlert className="size-10 text-danger" aria-hidden="true" />
        <h1 className="mt-3 text-xl font-bold text-ink">Truy cập bị từ chối (403)</h1>
        <p className="mt-2 text-sm text-ink-muted">Chỉ tài khoản Tác giả hoặc Quản trị viên mới tạo, sửa được công thức.</p>
        <Link
          href="/dashboard"
          className="mt-6 inline-flex h-10 items-center rounded-md bg-brand-600 px-4 text-sm font-medium text-on-brand hover:bg-brand-700"
        >
          Về Bảng điều khiển
        </Link>
      </div>
    );
  }

  return <>{children}</>;
}
