'use client';

import Link from 'next/link';
import { useSession } from 'next-auth/react';
import { LogoutButton } from '@/components/auth/LogoutButton';
import { Skeleton } from '@/components/ui';

/**
 * FR-AUTH-005 — vùng tài khoản trên header. Client component (useSession) để SiteHeader trong root
 * layout không đọc cookie phía server — nếu không mọi trang ISR (SRS 5.1) sẽ thành render động.
 * Phiên đã chết (session.error, FR-AUTH-004) coi như chưa đăng nhập; SessionExpiryWatcher lo phần còn lại.
 */
export function HeaderAuth() {
  const { data: session, status } = useSession();

  if (status === 'loading') {
    return (
      <div aria-busy="true" aria-label="Đang tải phiên đăng nhập">
        <Skeleton className="h-8 w-32" />
      </div>
    );
  }

  const profile = session && !session.error ? session.profile : undefined;

  if (!profile) {
    return (
      <div className="flex items-center gap-3 text-sm">
        <Link href="/auth/login" className="font-medium text-brand-700 hover:underline">
          Đăng nhập
        </Link>
        <Link
          href="/auth/register"
          className="rounded-md bg-brand-600 px-3 py-1.5 font-medium text-white hover:bg-brand-700"
        >
          Đăng ký
        </Link>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <span className="max-w-24 truncate text-sm font-medium text-ink sm:max-w-40" title={profile.displayName}>
        {profile.displayName}
      </span>
      {/* FR-AUTH-006 — trang xem hồ sơ cá nhân. */}
      <Link href="/dashboard/profile" className="text-sm font-medium text-brand-700 hover:underline">
        Hồ sơ
      </Link>
      <LogoutButton />
    </div>
  );
}
