/**
 * @file Trang 404 - Không tìm thấy danh mục.
 * @see NFR-USE-004 (Custom Not-Found UI)
 * @see NFR-USE-002 (Accessibility & ARIA)
 */

import React from "react";
import Link from "next/link";

export default function CategoryNotFound() {
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
            d="M4 6h16M4 10h16M4 14h16M4 18h16" 
          />
        </svg>
      </div>

      <h1 className="mb-3 text-2xl font-bold tracking-tight text-ink sm:text-3xl">
        Không tìm thấy danh mục
      </h1>
      
      <p className="mb-8 text-base text-ink-muted">
        Danh mục món ăn bạn đang tìm kiếm không tồn tại hoặc đã được gỡ bỏ khỏi hệ thống.
      </p>

      {/* Nhóm nút điều hướng */}
      <div className="flex flex-wrap items-center justify-center gap-4">
        <Link
          href="/categories"
          className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2"
        >
          <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h7" />
          </svg>
          Xem tất cả danh mục
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