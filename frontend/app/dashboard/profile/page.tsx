'use client';

import { Badge, Button, Skeleton } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { useCurrentUser } from '@/lib/hooks/useCurrentUser';

/**
 * FR-AUTH-006 — trang xem hồ sơ cá nhân. SRS 5.1: /dashboard/* render CSR.
 * D5: chỉ hiển thị displayName, email, avatarUrl, bio, roles (không có fullName/userName).
 */
export default function ProfilePage() {
  const { data: profile, isPending, isError, error, refetch } = useCurrentUser();

  return (
    <div className="mx-auto max-w-2xl px-4 py-10">
      <header className="border-b border-border pb-6">
        <h1 className="text-2xl font-bold text-brand-700">Hồ sơ của tôi</h1>
        <p className="mt-1 text-sm text-ink-muted">Thông tin tài khoản đang đăng nhập.</p>
      </header>

      <section aria-label="Thông tin hồ sơ" className="mt-6 rounded-lg border border-border bg-surface p-6">
        {isPending ? (
          <div aria-busy="true" aria-label="Đang tải hồ sơ" className="flex items-center gap-4">
            <Skeleton className="size-16 rounded-full" />
            <div className="flex flex-1 flex-col gap-2">
              <Skeleton className="h-5 w-1/2" />
              <Skeleton className="h-4 w-2/3" />
            </div>
          </div>
        ) : isError ? (
          <div role="alert" className="flex flex-col items-center gap-3 p-4 text-center text-sm text-red-700">
            <p>{toProblemDetails(error).detail ?? 'Không tải được hồ sơ.'}</p>
            <Button type="button" variant="outline" size="sm" onClick={() => refetch()}>
              Thử lại
            </Button>
          </div>
        ) : (
          <div className="flex flex-col gap-6">
            <div className="flex items-center gap-4">
              {profile.avatarUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- URL ngoài tùy ý người dùng, không cấu hình được next/image
                <img
                  src={profile.avatarUrl}
                  alt={`Ảnh đại diện của ${profile.displayName}`}
                  className="size-16 rounded-full object-cover"
                />
              ) : (
                <div
                  aria-hidden="true"
                  data-testid="avatar-fallback"
                  className="flex size-16 items-center justify-center rounded-full bg-brand-100 text-xl font-semibold text-brand-700"
                >
                  {profile.displayName.trim().charAt(0).toUpperCase()}
                </div>
              )}
              <div>
                <h2 className="text-lg font-semibold text-ink">{profile.displayName}</h2>
                <p className="text-sm text-ink-muted">{profile.email}</p>
              </div>
            </div>

            <dl className="flex flex-col gap-4 text-sm">
              <div>
                <dt className="font-medium text-ink-muted">Giới thiệu</dt>
                <dd className="mt-1 whitespace-pre-line text-ink">{profile.bio ?? 'Chưa có giới thiệu'}</dd>
              </div>
              <div>
                <dt className="font-medium text-ink-muted">Vai trò</dt>
                <dd className="mt-1 flex flex-wrap gap-2">
                  {profile.roles.map((role) => (
                    <Badge key={role}>{role}</Badge>
                  ))}
                </dd>
              </div>
            </dl>
          </div>
        )}
      </section>
    </div>
  );
}
