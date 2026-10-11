'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';

export default function AdminCategoriesLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const [isAuthorized, setIsAuthorized] = useState<boolean | null>(null);

  useEffect(() => {
    // 1. Kiểm tra session/token đăng nhập
    const userJson = localStorage.getItem('user') || sessionStorage.getItem('user');
    const token = localStorage.getItem('token') || sessionStorage.getItem('token');

    if (!userJson && !token) {
      // Chưa đăng nhập -> chuyển về login
      router.replace('/auth/login?callbackUrl=/dashboard/categories');
      return;
    }

    try {
      if (userJson) {
        const user = JSON.parse(userJson);
        const roles: string[] = user.roles || (user.role ? [user.role] : []);
        const isAdmin = roles.includes('Admin') || roles.includes('admin') || user.isAdmin === true;

        if (!isAdmin) {
          setIsAuthorized(false);
          return;
        }
      }
      setIsAuthorized(true);
    } catch {
      setIsAuthorized(false);
    }
  }, [router]);

  // Đang kiểm tra quyền
  if (isAuthorized === null) {
    return (
      <div className="flex min-h-[400px] items-center justify-center">
        <div className="text-center">
          <div className="size-8 animate-spin rounded-full border-4 border-orange-500 border-t-transparent mx-auto"></div>
          <p className="mt-3 text-sm text-slate-500">Đang kiểm tra quyền truy cập...</p>
        </div>
      </div>
    );
  }

  // Không có quyền Admin -> Hiện thông báo 403 Forbidden thay vì cho vào giao diện quản trị
  if (isAuthorized === false) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center px-4 text-center">
        <div className="rounded-full bg-red-100 p-3 text-red-600 mb-3">
          <svg className="size-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
        </div>
        <h2 className="text-xl font-bold text-slate-800">Truy cập bị từ chối (403)</h2>
        <p className="mt-2 text-sm text-slate-500 max-w-md">
          Trang quản trị danh mục chỉ dành cho tài khoản có quyền <strong>Quản trị viên (Admin)</strong>.
        </p>
        <button
          onClick={() => router.push('/')}
          className="mt-6 px-4 py-2 bg-slate-800 hover:bg-slate-900 text-white text-sm font-medium rounded-lg"
        >
          Trở về Trang chủ
        </button>
      </div>
    );
  }

  return <>{children}</>;
}