"use client";

import React, { useState } from "react";
import { CategoryDto, createCategory, updateCategory } from "@/lib/categories";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  categoryToEdit?: CategoryDto | null;
  onSuccess: () => void;
  token?: string;
}

// Component cha: Điều khiển ẩn/hiện và tự động reset form bằng key
export default function CategoryFormModal(props: Props) {
  if (!props.isOpen) return null;

  return (
    <CategoryFormModalContent
      key={props.categoryToEdit?.id ?? "create-new-category"}
      {...props}
    />
  );
}

// Component con: Khởi tạo state trực tiếp từ props mà KHÔNG CẦN useEffect
function CategoryFormModalContent({
  onClose,
  categoryToEdit,
  onSuccess,
  token,
}: Props) {
  const isEdit = !!categoryToEdit;
  const [name, setName] = useState(categoryToEdit?.name ?? "");
  const [description, setDescription] = useState(categoryToEdit?.description ?? "");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError("Tên danh mục không được để trống.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (isEdit && categoryToEdit) {
        // FR-CAT-004: Cập nhật danh mục
        await updateCategory(
          categoryToEdit.id,
          { name: name.trim(), description: description.trim() },
          token
        );
      } else {
        // FR-CAT-003: Tạo mới danh mục
        await createCategory(
          { name: name.trim(), description: description.trim() },
          token
        );
      }
      onSuccess();
      onClose();
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Đã xảy ra lỗi không xác định.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-gray-100 space-y-4">
        <div className="flex justify-between items-center border-b pb-3">
          <h3 className="text-lg font-bold text-gray-900">
            {isEdit ? "Cập Nhật Danh Mục (FR-CAT-004)" : "Tạo Mới Danh Mục (FR-CAT-003)"}
          </h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 font-bold p-1 text-base"
          >
            ✕
          </button>
        </div>

        {error && (
          <div className="p-3 bg-red-50 text-red-700 text-xs rounded-xl border border-red-200">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1">
              Tên Danh Mục <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="VD: Món nướng hải sản..."
              className="w-full px-3 py-2 border rounded-xl text-sm focus:ring-2 focus:ring-amber-500 focus:outline-hidden"
            />
          </div>

          {isEdit && (
            <div className="p-2.5 bg-amber-50 rounded-xl border border-amber-200 text-xs text-amber-800">
              🔒 <strong>Quy định D10:</strong> Slug hiện tại <code>/{categoryToEdit?.slug}</code> sẽ <strong>KHÔNG đổi</strong> khi thay đổi Name.
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1">Mô Tả</label>
            <textarea
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Mô tả về các công thức thuộc danh mục này..."
              className="w-full px-3 py-2 border rounded-xl text-sm focus:ring-2 focus:ring-amber-500 focus:outline-hidden resize-none"
            />
          </div>

          <div className="flex justify-end space-x-2 pt-2 border-t">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 rounded-xl"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={loading}
              className="px-4 py-2 text-sm font-semibold text-white bg-amber-600 hover:bg-amber-700 rounded-xl disabled:opacity-50"
            >
              {loading ? "Đang lưu..." : isEdit ? "Lưu Thay Đổi" : "Tạo Mới"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}