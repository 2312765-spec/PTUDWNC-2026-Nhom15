"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { CategoryDto, getCategories } from "@/lib/categories";
import CategoryFormModal from "@/components/categories/CategoryFormModal";

export default function CategoriesPage() {
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<CategoryDto | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const data = await getCategories();
      setCategories(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const filtered = categories.filter((c) =>
    c.name.toLowerCase().includes(search.toLowerCase()) ||
    c.description?.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="max-w-6xl mx-auto px-4 py-8 space-y-6">
      {/* Header Bar */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 border-b pb-5">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Danh Mục Công Thức (FR-CAT-001)</h1>
          <p className="text-gray-500 text-sm mt-1">
            Tổng hợp danh mục món ăn thơm ngon kèm số lượng công thức đã xuất bản.
          </p>
        </div>

        {/* Nút Tạo Danh Mục [Admin - FR-CAT-003] */}
        <button
          onClick={() => {
            setSelectedCategory(null);
            setIsModalOpen(true);
          }}
          className="px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-gray-950 font-bold rounded-xl text-sm transition-all shadow-sm"
        >
          + Thêm Danh Mục (Admin)
        </button>
      </div>

      {/* Ô tìm kiếm */}
      <div className="w-full sm:w-80">
        <input
          type="text"
          placeholder="Tìm theo tên danh mục..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full px-4 py-2 border rounded-xl text-sm focus:ring-2 focus:ring-amber-500 focus:outline-none"
        />
      </div>

      {/* Grid danh mục */}
      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-40 bg-gray-100 animate-pulse rounded-2xl border" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {filtered.map((cat) => (
            <div
              key={cat.id}
              className="bg-white p-5 rounded-2xl border border-gray-200 hover:border-amber-400 hover:shadow-md transition-all flex flex-col justify-between"
            >
              <div>
                <div className="flex justify-between items-center mb-2">
                  <span className="text-xs font-semibold px-2 py-0.5 bg-amber-100 text-amber-800 rounded-full">
                    {cat.recipeCount} công thức
                  </span>
                  <span className="text-[11px] text-gray-400 font-mono">/{cat.slug}</span>
                </div>
                <h3 className="font-bold text-gray-900 text-lg hover:text-amber-600 transition-colors">
                  <Link href={`/categories/${cat.slug}`}>{cat.name}</Link>
                </h3>
                <p className="text-gray-600 text-xs mt-1 line-clamp-2">
                  {cat.description || "Chưa có mô tả."}
                </p>
              </div>

              {/* Action Buttons */}
              <div className="pt-3 mt-4 border-t flex justify-between items-center text-xs">
                {/* Nút Cập Nhật [Admin - FR-CAT-004] */}
                <button
                  onClick={() => {
                    setSelectedCategory(cat);
                    setIsModalOpen(true);
                  }}
                  className="text-sky-600 hover:underline font-semibold"
                >
                  Sửa (Admin)
                </button>

                {/* Nút Xem Chi Tiết [FR-CAT-002] */}
                <Link
                  href={`/categories/${cat.slug}`}
                  className="text-amber-600 hover:underline font-semibold"
                >
                  Xem món &rarr;
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modal Form */}
      <CategoryFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        categoryToEdit={selectedCategory}
        onSuccess={loadData}
      />
    </div>
  );
}