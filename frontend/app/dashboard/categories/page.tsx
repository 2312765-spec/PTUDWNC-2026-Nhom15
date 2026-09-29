'use client';

import { useCallback, useEffect, useState } from 'react';
import { apiClient, toProblemDetails } from '@/lib/api-client';

interface CategoryItem {
  id: string;
  name: string;
  slug: string;
  description?: string;
  recipeCount: number;
}

export default function AdminCategoriesDashboardPage() {
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // State Modal (Dùng chung cho cả FR-CAT-003 Tạo và FR-CAT-004 Sửa)
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<CategoryItem | null>(null);
  const [formData, setFormData] = useState({ name: '', description: '' });
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  // State Xóa (FR-CAT-005)
  const [deletingCategory, setDeletingCategory] = useState<CategoryItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // 1. Tải danh sách danh mục (FR-CAT-001)
  const loadCategories = useCallback(async () => {
    try {
      setErrorMessage(null);
      const res = await apiClient.get<CategoryItem[]>('/categories');
      setCategories(res.data);
    } catch (err) {
      const problem = toProblemDetails(err);
      setErrorMessage(problem.detail || problem.title);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let ignore = false;
    async function init() {
      try {
        const res = await apiClient.get<CategoryItem[]>('/categories');
        if (!ignore) {
          setCategories(res.data);
          setLoading(false);
        }
      } catch (err) {
        if (!ignore) {
          const problem = toProblemDetails(err);
          setErrorMessage(problem.detail || problem.title);
          setLoading(false);
        }
      }
    }
    init();
    return () => {
      ignore = true;
    };
  }, []);

  // Mở form Tạo mới (FR-CAT-003)
  const handleOpenCreate = () => {
    setEditingCategory(null);
    setFormData({ name: '', description: '' });
    setFieldErrors({});
    setErrorMessage(null);
    setIsModalOpen(true);
  };

  // Mở form Chỉnh sửa (FR-CAT-004)
  const handleOpenEdit = (cat: CategoryItem) => {
    setEditingCategory(cat);
    setFormData({ name: cat.name, description: cat.description || '' });
    setFieldErrors({});
    setErrorMessage(null);
    setIsModalOpen(true);
  };

  // 2. Submit form Tạo hoặc Sửa (FR-CAT-003 / FR-CAT-004)
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setFieldErrors({});
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      if (editingCategory) {
        // FR-CAT-004: PUT /api/v1/categories/{id}
        await apiClient.put(`/categories/${editingCategory.id}`, {
          name: formData.name.trim(),
          description: formData.description.trim() || null,
        });
        setSuccessMessage(`Đã cập nhật danh mục "${formData.name}" thành công.`);
      } else {
        // FR-CAT-003: POST /api/v1/categories
        await apiClient.post('/categories', {
          name: formData.name.trim(),
          description: formData.description.trim() || null,
        });
        setSuccessMessage(`Đã tạo mới danh mục "${formData.name}" thành công.`);
      }

      setIsModalOpen(false);
      await loadCategories();
    } catch (err) {
      const problem = toProblemDetails(err);
      if (problem.errors) {
        setFieldErrors(problem.errors);
      } else {
        setErrorMessage(problem.detail || problem.title);
      }
    } finally {
      setSubmitting(false);
    }
  };

  // 3. Xử lý Xóa danh mục (FR-CAT-005)
  const handleDeleteConfirm = async () => {
    if (!deletingCategory) return;

    if (deletingCategory.recipeCount > 0) {
      setErrorMessage(`Không thể xóa danh mục "${deletingCategory.name}" vì vẫn còn ${deletingCategory.recipeCount} công thức.`);
      setDeletingCategory(null);
      return;
    }

    try {
      setIsDeleting(true);
      await apiClient.delete(`/categories/${deletingCategory.id}`);
      setSuccessMessage(`Đã xóa mềm danh mục "${deletingCategory.name}".`);
      setDeletingCategory(null);
      await loadCategories();
    } catch (err) {
      const problem = toProblemDetails(err);
      setErrorMessage(problem.detail || problem.title);
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      {/* Tiêu đề & Nút Thêm mới */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between pb-6 border-b border-border gap-4">
        <div>
          <h1 className="text-2xl font-bold text-brand-700">Quản lý Danh mục</h1>
          <p className="text-sm text-ink-muted mt-1">
            Bảng điều khiển Quản trị viên (Admin) — Thêm (CAT-003), Sửa (CAT-004), Xóa (CAT-005)
          </p>
        </div>
        <button
          type="button"
          onClick={handleOpenCreate}
          className="inline-flex items-center justify-center rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-brand-700 transition"
        >
          + Thêm danh mục mới
        </button>
      </div>

      {/* Thông báo Thành công / Thất bại */}
      {successMessage && (
        <div className="mt-4 rounded-lg bg-green-50 border border-green-200 p-4 text-sm text-green-700">
          {successMessage}
        </div>
      )}
      {errorMessage && (
        <div className="mt-4 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">
          {errorMessage}
        </div>
      )}

      {/* Bảng danh sách danh mục */}
      <div className="mt-6 overflow-hidden rounded-xl border border-border bg-surface shadow-sm">
        {loading ? (
          <div className="p-8 text-center text-sm text-ink-muted">Đang tải danh sách danh mục...</div>
        ) : categories.length === 0 ? (
          <div className="p-8 text-center text-sm text-ink-muted">Chưa có danh mục nào trong hệ thống.</div>
        ) : (
          <table className="min-w-full divide-y divide-border text-left text-sm">
            <thead className="bg-surface-muted text-ink-muted font-medium">
              <tr>
                <th className="px-6 py-3.5">Tên danh mục</th>
                <th className="px-6 py-3.5">Slug (Đường dẫn)</th>
                <th className="px-6 py-3.5 text-center">Số công thức</th>
                <th className="px-6 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {categories.map((cat) => (
                <tr key={cat.id} className="hover:bg-surface-muted/50 transition">
                  <td className="px-6 py-4 font-semibold text-ink">
                    {cat.name}
                    {cat.description && (
                      <p className="text-xs text-ink-muted font-normal mt-0.5 line-clamp-1">{cat.description}</p>
                    )}
                  </td>
                  <td className="px-6 py-4 font-mono text-xs text-ink-muted">{cat.slug}</td>
                  <td className="px-6 py-4 text-center">
                    <span className="inline-flex items-center rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-medium text-brand-700">
                      {cat.recipeCount}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right space-x-2">
                    <button
                      type="button"
                      onClick={() => handleOpenEdit(cat)}
                      className="rounded border border-border px-3 py-1 text-xs font-medium text-ink hover:bg-surface-muted transition"
                    >
                      Sửa
                    </button>
                    <button
                      type="button"
                      onClick={() => setDeletingCategory(cat)}
                      className="rounded border border-red-200 px-3 py-1 text-xs font-medium text-red-600 hover:bg-red-50 transition"
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* MODAL THÊM / SỬA (FR-CAT-003 / FR-CAT-004) */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-surface p-6 shadow-xl border border-border">
            <h2 className="text-lg font-bold text-ink mb-4">
              {editingCategory ? 'Chỉnh sửa danh mục (FR-CAT-004)' : 'Tạo danh mục mới (FR-CAT-003)'}
            </h2>

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-ink mb-1">
                  Tên danh mục <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  placeholder="Ví dụ: Món Ăn Sáng, Bánh Ngọt..."
                  className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none"
                />
                {fieldErrors.name && (
                  <p className="mt-1 text-xs text-red-600">{fieldErrors.name[0]}</p>
                )}
              </div>

              <div>
                <label className="block text-sm font-medium text-ink mb-1">Mô tả (tùy chọn)</label>
                <textarea
                  rows={3}
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  placeholder="Mô tả thông tin danh mục..."
                  className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none"
                />
              </div>

              <div className="mt-6 flex justify-end gap-3 pt-3 border-t border-border">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="rounded-lg bg-surface-muted px-4 py-2 text-sm font-medium text-ink hover:bg-border transition"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 transition disabled:opacity-50"
                >
                  {submitting ? 'Đang lưu...' : editingCategory ? 'Lưu thay đổi' : 'Tạo mới'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL XÁC NHẬN XÓA (FR-CAT-005) */}
      {deletingCategory && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-surface p-6 shadow-xl border border-border">
            <h3 className="text-lg font-bold text-ink">Xác nhận xóa danh mục</h3>
            <p className="mt-2 text-sm text-ink-muted">
              Bạn có chắc chắn muốn xóa danh mục <strong>&quot;{deletingCategory.name}&quot;</strong>?
              Hệ thống sẽ thực hiện xóa mềm (Soft delete) theo Quyết định D2.
            </p>

            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setDeletingCategory(null)}
                className="rounded-lg bg-surface-muted px-4 py-2 text-sm font-medium text-ink hover:bg-border transition"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isDeleting}
                onClick={handleDeleteConfirm}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 transition disabled:opacity-50"
              >
                {isDeleting ? 'Đang xóa...' : 'Đồng ý xóa'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}