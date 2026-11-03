/**
 * @file Skeleton loading state cho trang Danh sách danh mục.
 * @see NFR-USE-004
 */

import React from "react";
import {Skeleton} from "@/components/ui/Skeleton";

export default function CategoriesLoading() {
  return (
    <div 
      className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8" 
      role="status" 
      aria-label="Đang tải danh mục..."
    >
      <span className="sr-only">Đang tải danh mục...</span>

      <div className="mb-8" aria-hidden="true">
        <Skeleton className="mb-2 h-8 w-56" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </div>

      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4" aria-hidden="true">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 rounded-xl border border-border p-4 bg-white shadow-sm">
            <Skeleton circle className="h-14 w-14 shrink-0" />
            <div className="flex-1">
              <Skeleton className="mb-2 h-5 w-3/4" />
              <Skeleton className="h-4 w-1/2" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}