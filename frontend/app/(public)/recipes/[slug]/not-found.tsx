/**
 * @file Trang 404 - Không tìm thấy công thức nấu ăn.
 * @see NFR-USE-004 (Custom Not-Found UI)
 * @see NFR-USE-002 (Accessibility & ARIA)
 * @see D13 (Slug immutability - No 301 redirects)
 */

import React from "react";
import Link from "next/link";

export default function RecipeNotFound() {
  return (
    <main className="mx-auto flex min-h-[60vh] max-w-2xl flex-col items-center justify-center px-4 py-16 text-center sm:px-6">
      {/* Icon trang trí */}
      <div 
        className="mb-6 flex h-20 w-20 items-center justify-center rounded-full bg-brand-50 text-brand-600 border border-brand-200"
        aria-hidden="true"
      >
        <svg 
          className="h-10 w-10" 
          fill="none" 
          stroke="currentColor" 
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <path 
            strokeLinecap="round" 
            strokeLinejoin="round" 
            strokeWidth={1.5} 
            d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" 
          />
        </svg>
      </div>

      <h1 className="mb-3 text-2xl font-bold tracking-tight text-ink sm:text-3xl">
        Không tìm thấy công thức
      </h1>
      
      <p className="mb-8 text-base text-ink-muted">
        Công thức nấu ăn bạn đang tìm kiếm không tồn tại hoặc đã được gỡ bỏ khỏi hệ thống.
      </p>

      {/* Nhóm nút điều hướng */}
      <div className="flex flex-wrap items-center justify-center gap-4">
        <Link
          href="/recipes"
          className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2"
        >
          <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H7.414l5.293-5.293a1 1 0 00-1.414-1.414l-7 7a1 1 0 000 1.414l7 7a1 1 0 001.414-1.414L7.414 13H19a1 1 0 001-1z" />
          </svg>
          Khám phá công thức khác
        </Link>
        <Link
          href="/"
          className="inline-flex items-center rounded-lg border border-border bg-white px-5 py-2.5 text-sm font-medium text-ink transition-colors hover:bg-subtle focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2"
        >
          Về trang chủ
        </Link>
      </div>
    </main>
  );
}