/**
 * @file Skeleton loading state dành riêng cho Trang chủ (Home).
 * @see NFR-USE-004 (Skeleton Loading)
 * @see NFR-USE-002 (Accessibility & ARIA Status)
 */

import React from "react";
import {Skeleton} from "@/components/ui/Skeleton";
import RecipeCardSkeleton from "@/components/recipes/RecipeCardSkeleton";

export default function HomeLoading() {
  return (
    <div 
      className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8" 
      role="status" 
      aria-label="Đang tải dữ liệu trang chủ..."
    >
      <span className="sr-only">Đang tải trang chủ...</span>

      {/* Hero Section Skeleton */}
      <div className="mb-12 text-center" aria-hidden="true">
        <Skeleton className="mx-auto mb-4 h-10 w-3/4 max-w-2xl sm:h-12" />
        <Skeleton className="mx-auto mb-8 h-5 w-2/3 max-w-xl" />
        
        {/* Thanh tìm kiếm */}
        <div className="mx-auto max-w-xl">
          <Skeleton className="h-12 w-full rounded-full" />
        </div>
      </div>

      {/* Danh mục nổi bật */}
      <div className="mb-12" aria-hidden="true">
        <Skeleton className="mb-6 h-7 w-48" />
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="flex flex-col items-center gap-2 p-3">
              <Skeleton circle className="h-14 w-14" />
              <Skeleton className="h-4 w-16" />
            </div>
          ))}
        </div>
      </div>

      {/* Lưới công thức nổi bật */}
      <div aria-hidden="true">
        <div className="mb-6 flex items-center justify-between">
          <Skeleton className="h-7 w-56" />
          <Skeleton className="h-5 w-24" />
        </div>
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <RecipeCardSkeleton key={i} />
          ))}
        </div>
      </div>
    </div>
  );
}