/**
 * @file Skeleton loading state cho trang Chi tiết công thức nấu ăn.
 * @see NFR-USE-004 (Skeleton Loading)
 * @see NFR-USE-002 (Accessibility & ARIA Status)
 * @see NFR-PERF-005 (Prevent Cumulative Layout Shift)
 */

import React from "react";
import {Skeleton }from "@/components/ui/Skeleton";

export default function RecipeDetailLoading() {
  return (
    <article 
      className="mx-auto max-w-4xl px-4 py-8 sm:px-6 lg:px-8" 
      role="status" 
      aria-label="Đang tải chi tiết công thức..."
    >
      <span className="sr-only">Đang tải chi tiết công thức...</span>

      <div aria-hidden="true">
        {/* Breadcrumb skeleton */}
        <div className="mb-6 flex items-center gap-2">
          <Skeleton className="h-4 w-16" />
          <span className="text-border">/</span>
          <Skeleton className="h-4 w-20" />
          <span className="text-border">/</span>
          <Skeleton className="h-4 w-32" />
        </div>

        {/* Tiêu đề công thức & Mô tả ngắn */}
        <Skeleton className="mb-4 h-9 w-4/5 sm:h-11" />
        <Skeleton className="mb-6 h-5 w-full max-w-2xl" />

        {/* Thông tin tác giả & Metadata chips */}
        <div className="mb-8 flex flex-wrap items-center justify-between gap-4 border-y border-border py-4">
          <div className="flex items-center gap-3">
            <Skeleton circle className="h-10 w-10" />
            <div>
              <Skeleton className="mb-1 h-4 w-28" />
              <Skeleton className="h-3 w-20" />
            </div>
          </div>
          <div className="flex items-center gap-3">
            <Skeleton className="h-8 w-24 rounded-full" />
            <Skeleton className="h-8 w-24 rounded-full" />
            <Skeleton className="h-8 w-24 rounded-full" />
          </div>
        </div>

        {/* Ảnh đại diện chính (Aspect ratio 16:9) */}
        <div className="mb-10 overflow-hidden rounded-2xl border border-border">
          <Skeleton className="aspect-video w-full" />
        </div>

        {/* Bố cục nội dung chính: Nguyên liệu & Các bước */}
        <div className="grid grid-cols-1 gap-10 md:grid-cols-3">
          {/* Cột trái: Nguyên liệu */}
          <div className="md:col-span-1">
            <div className="rounded-xl border border-border p-5">
              <Skeleton className="mb-4 h-6 w-36" />
              <div className="space-y-3">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <Skeleton className="h-4 w-4 rounded" />
                    <Skeleton className="h-4 flex-1" />
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Cột phải: Các bước thực hiện */}
          <div className="space-y-6 md:col-span-2">
            <Skeleton className="mb-4 h-6 w-40" />
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="rounded-xl border border-border p-5">
                <div className="mb-3 flex items-center gap-3">
                  <Skeleton circle className="h-7 w-7" />
                  <Skeleton className="h-5 w-32" />
                </div>
                <Skeleton className="mb-2 h-4 w-full" />
                <Skeleton className="h-4 w-5/6" />
              </div>
            ))}
          </div>
        </div>
      </div>
    </article>
  );
}