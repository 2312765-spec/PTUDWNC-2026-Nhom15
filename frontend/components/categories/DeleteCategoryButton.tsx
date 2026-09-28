"use client";

import { useState } from 'react';
import { apiClient } from '@/lib/api-client';

const modules = [
  { owner: 'A', name: 'Xác thực & Tài khoản', routes: '/auth/login, /auth/register, /profile' },
  { owner: 'B', name: 'Khám phá & Tìm kiếm', routes: '/, /recipes, /categories, /search' },
  { owner: 'C', name: 'Sáng tạo Công thức', routes: '/dashboard/recipes/*' },
  { owner: 'D', name: 'Media & Vận hành', routes: 'upload, gallery, SEO, sitemap' },
];

export default function HomePage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-16">
      <h1 className="text-3xl font-bold text-brand-700">Culinary Blog</h1>
      <p className="mt-2 text-ink-muted">
        Khung dự án đã chạy. Mỗi người mở thư mục của mình và bắt đầu từ Sprint 0.
      </p>

      <ul className="mt-8 space-y-3">
        {modules.map((m) => (
          <li key={m.owner} className="rounded-lg border border-border bg-surface p-4">
            <span className="inline-flex size-7 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
              {m.owner}
            </span>
            <span className="ml-3 font-medium">{m.name}</span>
            <p className="mt-1 text-sm text-ink-muted">{m.routes}</p>
          </li>
        ))}
      </ul>

      <p className="mt-8 text-sm text-ink-muted">
        Tài liệu: <code>docs/team-assignment.md</code> · <code>docs/roadmap.md</code> ·{' '}
        <code>docs/decisions.md</code>
      </p>
    </div>
  );
}

/**
 * Component nút xóa danh mục (FR-CAT-005)
 * Tuân thủ Quyết định D2: Kiểm tra recipeCount > 0 trước khi xóa.
 */
export function DeleteCategoryButton({
  categoryId,
  categoryName,
  recipeCount = 0,
  onSuccess,
}: {
  categoryId: string;
  categoryName: string;
  recipeCount?: number;
  onSuccess?: () => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleDelete = async () => {
    // 1. Kiểm tra ràng buộc nghiệp vụ: Không được xóa danh mục còn chứa công thức
    if (recipeCount > 0) {
      alert(`Danh mục còn chứa ${recipeCount} công thức. Bạn phải chuyển tất cả công thức sang danh mục khác trước khi xóa.`);
      setIsOpen(false);
      return;
    }

    try {
      setIsLoading(true);
      // 2. Gửi request DELETE /api/v1/categories/{id}
      await apiClient.delete(`/api/v1/categories/${categoryId}`);
      alert(`Đã xóa danh mục "${categoryName}" thành công.`);
      setIsOpen(false);
      onSuccess?.();
    } catch (err: any) {
      const errorDetail = err.response?.data?.detail || err.response?.data?.message || "Không thể xóa danh mục này.";
      alert(`Lỗi: ${errorDetail}`);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        className="rounded border border-red-200 px-3 py-1 text-sm font-medium text-red-600 hover:bg-red-50 hover:text-red-700"
      >
        Xóa
      </button>

      {isOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h3 className="text-lg font-semibold text-gray-900">Xác nhận xóa danh mục</h3>
            <p className="mt-2 text-sm text-gray-600">
              Bạn có chắc chắn muốn xóa danh mục <strong>"{categoryName}"</strong> không? Thao tác này sẽ xóa mềm danh mục khỏi hệ thống.
            </p>

            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setIsOpen(false)}
                className="rounded bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isLoading}
                onClick={handleDelete}
                className="rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {isLoading ? "Đang xóa..." : "Xác nhận xóa"}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}