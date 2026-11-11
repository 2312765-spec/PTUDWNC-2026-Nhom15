'use client';

import { usePathname } from 'next/navigation';
import SearchForm from '@/components/search/SearchForm';

/**
 * D97 / FR-SRCH-001 — ô tìm kiếm chung trên thanh bên. Ẩn ở /search: trang đó đã có ô tìm kiếm riêng (giữ từ khóa
 * `?q=`), mỗi trang chỉ một vùng role="search" để không trùng nhãn "Từ khóa tìm kiếm".
 */
export function SidebarSearch() {
  const pathname = usePathname();
  if (pathname === '/search') {
    return null;
  }
  return <SearchForm initialQuery="" stacked />;
}
