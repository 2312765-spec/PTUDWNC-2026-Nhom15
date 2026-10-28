'use client';

import { BookOpen, FolderTree, UserRound } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { useEffect, type ComponentType } from 'react';
import { Card, CardBody, Skeleton } from '@/components/ui';

const DASHBOARD_PATH = '/dashboard';

interface Shortcut {
  href: string;
  label: string;
  description: string;
  icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>;
}

const baseShortcuts: Shortcut[] = [
  { href: '/dashboard/profile', label: 'Hồ sơ của tôi', description: 'Xem và chỉnh sửa thông tin tài khoản.', icon: UserRound },
  { href: '/recipes', label: 'Khám phá công thức', description: 'Xem các món ăn đã được đăng.', icon: BookOpen },
];

const adminShortcuts: Shortcut[] = [
  { href: '/dashboard/categories', label: 'Quản lý danh mục', description: 'Thêm, sửa, xóa danh mục món ăn.', icon: FolderTree },
];

/**
 * FR-JOB-001/D12 — trang đích của link `/dashboard` trong email chào mừng. SRS 5.1: /dashboard/* render CSR.
 * Chỉ là khung chào mừng + lối tắt; phần "tổng quan Author" thuộc FR-RCP (C) — xem app/dashboard/OWNER.md.
 * Đọc phiên Auth.js (không gọi thêm API, giống HeaderAuth). Chỉ là UX — backend vẫn tự kiểm quyền (NFR-SEC-006).
 */
export default function DashboardPage() {
  const router = useRouter();
  const { data: session, status } = useSession();

  useEffect(() => {
    if (status === 'unauthenticated') {
      router.replace(`/auth/login?callbackUrl=${encodeURIComponent(DASHBOARD_PATH)}`);
    }
  }, [status, router]);

  const profile = status === 'authenticated' && !session?.error ? session?.profile : undefined;

  // Đang tải phiên, hoặc đang chuyển sang trang đăng nhập.
  if (!profile) {
    return (
      <div aria-busy="true" aria-label="Đang kiểm tra phiên đăng nhập" className="mx-auto max-w-3xl px-4 py-10">
        <Skeleton className="h-8 w-72" />
        <Skeleton className="mt-6 h-24 w-full" />
      </div>
    );
  }

  const shortcuts = profile.roles.includes('Admin') ? [...baseShortcuts, ...adminShortcuts] : baseShortcuts;

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <header className="border-b border-border pb-6">
        <h1 className="text-2xl font-bold text-brand-700">Chào mừng, {profile.displayName}!</h1>
        <p className="mt-1 text-sm text-ink-muted">Bắt đầu từ một trong các lối tắt dưới đây.</p>
      </header>

      <nav aria-label="Lối tắt" className="mt-6 grid gap-4 sm:grid-cols-2">
        {shortcuts.map(({ href, label, description, icon: Icon }) => (
          <Link key={href} href={href} className="rounded-lg focus-visible:outline-2 focus-visible:outline-brand-600">
            <Card className="h-full transition-colors hover:border-brand-600">
              <CardBody className="flex items-start gap-3">
                <Icon className="mt-0.5 size-5 shrink-0 text-brand-600" aria-hidden />
                <div>
                  <p className="font-semibold text-ink">{label}</p>
                  <p className="mt-1 text-sm text-ink-muted">{description}</p>
                </div>
              </CardBody>
            </Card>
          </Link>
        ))}
      </nav>
    </div>
  );
}
