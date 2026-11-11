import Link from 'next/link';
import { HeaderAuth } from '@/components/auth/HeaderAuth';
import { SidebarSearch } from '@/components/layout/SidebarSearch';
import { SiteLogo } from '@/components/layout/SiteLogo';
import { getCategories } from '@/lib/categories';
import type { CategoryDto } from '@/lib/types';

const NAV_LINKS = [
  { href: '/recipes', label: 'Công thức' },
  { href: '/categories', label: 'Danh mục' },
  { href: '/search', label: 'Tìm kiếm' },
] as const;

/** Lỗi tải danh mục chỉ ẩn phần Mục lục, không làm sập mọi trang (NFR-USE-003). */
async function loadCategories(): Promise<CategoryDto[]> {
  try {
    return await getCategories();
  } catch {
    return [];
  }
}

function TableOfContents({ categories }: { categories: CategoryDto[] }) {
  return (
    <ol className="flex flex-col">
      {categories.map((cat, index) => (
        <li key={cat.id}>
          <Link
            href={`/categories/${cat.slug}`}
            className="flex min-h-10 items-baseline gap-3 border-b border-dashed border-border py-2 text-ink hover:text-accent"
          >
            <span className="w-6 font-display text-sm font-bold text-ink-muted">{String(index + 1).padStart(2, '0')}</span>
            <span>{cat.name}</span>
          </Link>
        </li>
      ))}
    </ol>
  );
}

/**
 * D97 — thanh bên kiểu mục lục sổ tay, thay header ngang (root layout). Màn ≥ lg (1200px — D84): cột trái cố định;
 * màn nhỏ hơn: dải trên cùng, Mục lục thu vào <details> (không cần JS, dùng được bằng bàn phím).
 *
 * Server Component, KHÔNG gọi auth() để giữ ISR (SRS 5.1) — phần phụ thuộc phiên nằm trong HeaderAuth (client).
 * Danh mục lấy qua getCategories() (revalidate 3600, cùng cache với /categories). Giữ nguyên chữ trên link điều
 * hướng và aria-label (E2E, NFR-USE-002). FR-AUTH-005: vùng tài khoản ở cuối.
 */
export async function SiteSidebar() {
  const categories = await loadCategories();

  return (
    <header className="border-b border-border bg-surface-sunken lg:sticky lg:top-0 lg:h-screen lg:w-72 lg:flex-none lg:overflow-y-auto lg:border-b-0 lg:border-r">
      <div className="flex flex-col gap-4 px-4 py-4 lg:min-h-full lg:gap-8 lg:px-7 lg:py-8">
        <SiteLogo />

        <SidebarSearch />

        <nav aria-label="Điều hướng chính">
          <ul className="flex flex-wrap gap-x-1 gap-y-1 lg:flex-col">
            {NAV_LINKS.map((link) => (
              <li key={link.href}>
                <Link
                  href={link.href}
                  className="inline-flex min-h-10 items-center rounded-md px-3 text-ink-muted hover:bg-surface hover:text-ink lg:flex"
                >
                  {link.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        {categories.length > 0 && (
          <>
            <nav aria-label="Mục lục" className="hidden lg:block">
              <p className="mb-2 text-xs font-bold uppercase tracking-[0.18em] text-accent">Mục lục</p>
              <TableOfContents categories={categories} />
            </nav>
            <details className="group lg:hidden">
              <summary className="inline-flex min-h-10 cursor-pointer items-center gap-2 rounded-md border border-border px-3 font-semibold text-ink">
                Mục lục
                <span aria-hidden="true" className="transition-transform group-open:rotate-180">
                  ▾
                </span>
              </summary>
              <nav aria-label="Mục lục" className="mt-2">
                <TableOfContents categories={categories} />
              </nav>
            </details>
          </>
        )}

        <div className="lg:mt-auto">
          <HeaderAuth />
        </div>
      </div>
    </header>
  );
}
