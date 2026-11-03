/**
 * @file Skeleton loading state cho trang Chi tiết công thức — cùng bố cục với page.tsx
 * (breadcrumb, header, ảnh, thông số, nguyên liệu + các bước) để không nhảy layout khi tải xong.
 * @see NFR-USE-004 (Skeleton Loading)
 * @see NFR-USE-002 (Accessibility & ARIA Status)
 */

import { Skeleton } from "@/components/ui/Skeleton";

export default function RecipeDetailLoading() {
  return (
    <div
      className="mx-auto max-w-4xl px-4 py-8"
      role="status"
      aria-label="Đang tải chi tiết công thức..."
    >
      <span className="sr-only">Đang tải chi tiết công thức...</span>

      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2" aria-hidden="true">
        <Skeleton className="h-4 w-16" />
        <Skeleton className="h-4 w-24" />
        <Skeleton className="h-4 w-40" />
      </div>

      {/* Header: tiêu đề, mô tả, tác giả */}
      <div className="mb-8" aria-hidden="true">
        <Skeleton className="mb-4 h-10 w-4/5" />
        <Skeleton className="mb-2 h-5 w-full" />
        <Skeleton className="mb-6 h-5 w-3/5" />
        <div className="flex items-center gap-4 border-y border-border py-3">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-4 w-20" />
        </div>
      </div>

      {/* Ảnh chính */}
      <Skeleton className="mb-8 h-[350px] w-full rounded-xl md:h-[450px]" />

      {/* Thông số: chuẩn bị, nấu, khẩu phần, độ khó */}
      <div className="mb-10 grid grid-cols-2 gap-4 rounded-xl border border-border p-4 md:grid-cols-4" aria-hidden="true">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="flex flex-col items-center gap-2">
            <Skeleton className="h-3 w-16" />
            <Skeleton className="h-6 w-20" />
          </div>
        ))}
      </div>

      {/* Nguyên liệu + các bước */}
      <div className="grid grid-cols-1 gap-10 md:grid-cols-3" aria-hidden="true">
        <div className="rounded-xl border border-border p-6 md:col-span-1">
          <Skeleton className="mb-4 h-6 w-32" />
          <div className="space-y-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="flex items-center justify-between">
                <Skeleton className="h-4 w-28" />
                <Skeleton className="h-4 w-12" />
              </div>
            ))}
          </div>
        </div>

        <div className="space-y-6 md:col-span-2">
          <Skeleton className="h-7 w-44" />
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="space-y-3 rounded-xl border border-border p-5">
              <div className="flex items-center gap-3">
                <Skeleton circle className="h-8 w-8" />
                <Skeleton className="h-5 w-40" />
              </div>
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-5/6" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
