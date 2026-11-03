/**
 * @file Component khung xương (Skeleton) cho thẻ công thức nấu ăn.
 * @see NFR-USE-004 (Skeleton Loading & Component Reusability)
 * @see NFR-USE-002 (Accessibility & Screen Readers)
 */

import React from "react";
import {Skeleton} from "@/components/ui/Skeleton";

export function RecipeCardSkeleton() {
  return (
    <div 
      className="flex flex-col overflow-hidden rounded-xl border border-border bg-white shadow-sm"
      aria-hidden="true"
    >
      {/* Khung ảnh tỷ lệ 16:9 */}
      <Skeleton className="aspect-video w-full" />
      
      {/* Nội dung thông tin thẻ */}
      <div className="flex flex-1 flex-col p-4">
        {/* Badge danh mục */}
        <div className="mb-2 flex items-center gap-2">
          <Skeleton className="h-4 w-20 rounded-full" />
        </div>

        {/* Tiêu đề công thức 2 dòng */}
        <Skeleton className="mb-2 h-5 w-4/5" />
        <Skeleton className="mb-3 h-4 w-2/3" />

        {/* Thông tin phụ: Tác giả + Thời gian */}
        <div className="mt-auto flex items-center justify-between pt-3 border-t border-border">
          <div className="flex items-center gap-2">
            <Skeleton circle className="h-6 w-6" />
            <Skeleton className="h-3 w-16" />
          </div>
          <Skeleton className="h-3 w-12" />
        </div>
      </div>
    </div>
  );
}

export default RecipeCardSkeleton;