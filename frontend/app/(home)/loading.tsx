/**
 * @file Skeleton loading state dành riêng cho Trang chủ (Home) — cùng bố cục với page.tsx
 * (hero + tìm kiếm, 6 danh mục, 6 công thức mới nhất) để không nhảy layout khi tải xong.
 * @see NFR-USE-004 (Skeleton Loading)
 * @see NFR-USE-002 (Accessibility & ARIA Status)
 */

import { Skeleton } from "@/components/ui/Skeleton";
import RecipeCardSkeleton from "@/components/recipes/RecipeCardSkeleton";

export default function HomeLoading() {
  return (
    <div role="status" aria-label="Đang tải dữ liệu trang chủ...">
      <span className="sr-only">Đang tải trang chủ...</span>

      {/* Hero + thanh tìm kiếm */}
      <div className="border-b border-border bg-brand-50 py-16 md:py-24" aria-hidden="true">
        <div className="mx-auto max-w-5xl px-4">
          <Skeleton className="mx-auto h-10 w-3/4 max-w-2xl md:h-12" />
          <Skeleton className="mx-auto mt-4 h-5 w-2/3 max-w-xl" />
          <Skeleton className="mx-auto mt-8 h-12 w-full max-w-xl" />
        </div>
      </div>

      <div className="mx-auto max-w-6xl space-y-16 px-4 py-12" aria-hidden="true">
        {/* Danh mục */}
        <div>
          <Skeleton className="mb-6 h-8 w-40" />
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
            {Array.from({ length: 6 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full rounded-lg" />
            ))}
          </div>
        </div>

        {/* Công thức mới nhất */}
        <div>
          <div className="mb-6 flex items-center justify-between">
            <Skeleton className="h-8 w-56" />
            <Skeleton className="h-5 w-28" />
          </div>
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <RecipeCardSkeleton key={i} />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
