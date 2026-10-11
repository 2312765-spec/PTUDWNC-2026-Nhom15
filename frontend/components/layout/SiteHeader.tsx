import Link from 'next/link';
import { HeaderAuth } from '@/components/auth/HeaderAuth';

const NAV_LINKS = [
  { href: '/recipes', label: 'Công thức' },
  { href: '/categories', label: 'Danh mục' },
  { href: '/search', label: 'Tìm kiếm' },
] as const;

/**
 * Header chung của mọi trang (root layout). Server Component tĩnh — KHÔNG gọi auth() ở đây để
 * không làm mất ISR của các trang (SRS 5.1); phần phụ thuộc phiên nằm trong HeaderAuth (client).
 * FR-AUTH-005: nút Đăng xuất. NFR-USE-002: landmark header/nav có nhãn.
 */
export function SiteHeader() {
  return (
    <header className="border-b border-border bg-surface">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-3">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <Link href="/" className="text-lg font-bold text-brand-700">
            Culinary Blog
          </Link>
          <nav aria-label="Điều hướng chính">
            <ul className="flex flex-wrap items-center gap-4 text-sm">
              {NAV_LINKS.map((link) => (
                <li key={link.href}>
                  <Link href={link.href} className="text-ink-muted hover:text-ink">
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
        </div>
        <HeaderAuth />
      </div>
    </header>
  );
}
