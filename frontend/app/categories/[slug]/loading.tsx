/**
 * @file Skeleton loading state cho trang Chi tiết danh mục kèm danh sách công thức.
 * @see NFR-USE-004
 */

import React from "react";
import {Skeleton} from "@/components/ui/Skeleton";
import RecipeCardSkeleton from "@/components/recipes/RecipeCardSkeleton";

export default function CategoryDetailLoading() {
  return (
    <div 
      className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8" 
      role="status" 
      aria-label="Đang tải danh mục công thức..."
    >
      <span className="sr-only">Đang tải danh mục công thức...</span>

      <div className="mb-8" aria-hidden="true">
        <Skeleton className="mb-2 h-4 w-28" />
        <Skeleton className="mb-3 h-8 w-64" />
        <Skeleton className="h-4 w-80 max-w-full" />
      </div>

      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4" aria-hidden="true">
        {Array.from({ length: 8 }).map((_, i) => (
          <RecipeCardSkeleton key={i} />
        ))}
      </div>
    </div>
  );
}