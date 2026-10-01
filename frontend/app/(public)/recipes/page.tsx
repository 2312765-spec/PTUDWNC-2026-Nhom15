'use client';

import { useEffect, useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { apiClient, toProblemDetails } from '@/lib/api-client';

interface RecipeSummary {
  id: string;
  title: string;
  slug: string;
  description?: string;
  thumbnailUrl?: string;
  status: string;
  prepTime: number;
  cookTime: number;
  difficulty: string;
  createdAt: string;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

interface CategoryOption {
  id: string;
  name: string;
}

export default function RecipesPage() {
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // FR-SRCH-004: State Phân trang (default page=1, pageSize=12)
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // FR-SRCH-002: State Bộ lọc (categoryId, difficulty, maxCookTime, minServings)
  const [categoryId, setCategoryId] = useState('');
  const [difficulty, setDifficulty] = useState('');
  const [maxCookTime, setMaxCookTime] = useState('');
  const [minServings, setMinServings] = useState('');

  // FR-SRCH-003: State Sắp xếp (mặc định -createdAt)
  const [sort, setSort] = useState('-createdAt');

  // 1. Tải danh mục phục vụ bộ lọc
  useEffect(() => {
    let ignore = false;
    async function loadCategories() {
      try {
        const res = await apiClient.get<CategoryOption[]>('/categories');
        if (!ignore) {
          setCategories(res.data);
        }
      } catch {
        // bỏ qua lỗi nếu categories chưa có
      }
    }
    loadCategories();
    return () => {
      ignore = true;
    };
  }, []);

  // 2. Tải danh sách công thức kết hợp lọc, sắp xếp, phân trang (Sạch lỗi linter)
  useEffect(() => {
    let ignore = false;

    async function loadRecipes() {
      setLoading(true);
      setErrorMessage(null);

      try {
        const params = new URLSearchParams();
        params.append('page', page.toString());
        params.append('pageSize', '12');

        if (categoryId) params.append('categoryId', categoryId);
        if (difficulty) params.append('difficulty', difficulty);
        if (maxCookTime) params.append('maxCookTime', maxCookTime);
        if (minServings) params.append('minServings', minServings);
        if (sort) params.append('sort', sort);

        const res = await apiClient.get<PagedResult<RecipeSummary>>(`/recipes?${params.toString()}`);
        if (!ignore) {
          setRecipes(res.data.items || []);
          setTotalPages(res.data.totalPages || 1);
          setTotalCount(res.data.totalCount || 0);
        }
      } catch (err) {
        if (!ignore) {
          const problem = toProblemDetails(err);
          setErrorMessage(problem.detail || problem.title);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    loadRecipes();

    return () => {
      ignore = true;
    };
  }, [page, categoryId, difficulty, maxCookTime, minServings, sort]);

  // Reset trang về 1 khi đổi bộ lọc hoặc sắp xếp
  const handleFilterChange = (setter: (val: string) => void, val: string) => {
    setter(val);
    setPage(1);
  };

  const handleResetFilters = () => {
    setCategoryId('');
    setDifficulty('');
    setMaxCookTime('');
    setMinServings('');
    setSort('-createdAt');
    setPage(1);
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      {/* Tiêu đề */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between pb-6 border-b border-border gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-brand-700">Khám Phá Công Thức Nấu Ăn</h1>
          <p className="mt-1 text-sm text-ink-muted">
            Tìm kiếm, lọc và phân loại hàng ngàn món ăn ngon miệng từ cộng đồng ẩm thực
          </p>
        </div>
        <div className="text-sm text-ink-muted">
          Tìm thấy <strong className="text-ink font-semibold">{totalCount}</strong> công thức
        </div>
      </div>

      {/* THANH BỘ LỌC (FR-SRCH-002) & SẮP XẾP (FR-SRCH-003) */}
      <div className="mt-6 rounded-2xl border border-border bg-surface p-5 shadow-sm">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5">
          {/* Lọc theo Danh mục */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-ink-muted mb-1.5">
              Danh mục
            </label>
            <select
              value={categoryId}
              onChange={(e) => handleFilterChange(setCategoryId, e.target.value)}
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none"
            >
              <option value="">Tất cả danh mục</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>

          {/* Lọc theo Độ khó */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-ink-muted mb-1.5">
              Độ khó
            </label>
            <select
              value={difficulty}
              onChange={(e) => handleFilterChange(setDifficulty, e.target.value)}
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none"
            >
              <option value="">Tất cả độ khó</option>
              <option value="Easy">Dễ (Easy)</option>
              <option value="Medium">Trung bình (Medium)</option>
              <option value="Hard">Khó (Hard)</option>
            </select>
          </div>

          {/* Lọc Thời gian nấu tối đa */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-ink-muted mb-1.5">
              Thời gian nấu (≤ phút)
            </label>
            <input
              type="number"
              min="0"
              placeholder="VD: 30, 60..."
              value={maxCookTime}
              onChange={(e) => handleFilterChange(setMaxCookTime, e.target.value)}
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none"
            />
          </div>

          {/* Lọc Khẩu phần tối thiểu */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-ink-muted mb-1.5">
              Khẩu phần (≥ người)
            </label>
            <input
              type="number"
              min="1"
              placeholder="VD: 2, 4..."
              value={minServings}
              onChange={(e) => handleFilterChange(setMinServings, e.target.value)}
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none"
            />
          </div>

          {/* Sắp xếp (FR-SRCH-003) */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-ink-muted mb-1.5">
              Sắp xếp theo
            </label>
            <select
              value={sort}
              onChange={(e) => handleFilterChange(setSort, e.target.value)}
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none font-medium"
            >
              <option value="-createdAt">Mới nhất (Mặc định)</option>
              <option value="createdAt">Cũ nhất</option>
              <option value="title">Tên A → Z</option>
              <option value="-title">Tên Z → A</option>
              <option value="cookTime">Thời gian nấu ngắn nhất</option>
              <option value="-cookTime">Thời gian nấu lâu nhất</option>
            </select>
          </div>
        </div>

        {/* Nút đặt lại bộ lọc */}
        {(categoryId || difficulty || maxCookTime || minServings || sort !== '-createdAt') && (
          <div className="mt-4 flex justify-end">
            <button
              type="button"
              onClick={handleResetFilters}
              className="text-xs font-medium text-brand-600 hover:text-brand-800 underline transition cursor-pointer"
            >
              Xóa tất cả bộ lọc
            </button>
          </div>
        )}
      </div>

      {/* Thông báo lỗi */}
      {errorMessage && (
        <div className="mt-6 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">
          {errorMessage}
        </div>
      )}

      {/* DANH SÁCH CÔNG THỨC (FR-RCP-001) */}
      <div className="mt-8">
        {loading ? (
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {[...Array(8)].map((_, i) => (
              <div key={i} className="h-64 rounded-2xl bg-surface-muted animate-pulse border border-border" />
            ))}
          </div>
        ) : recipes.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-border p-12 text-center">
            <p className="text-base text-ink-muted">Không tìm thấy công thức nào khớp với tiêu chí lọc.</p>
            <button
              type="button"
              onClick={handleResetFilters}
              className="mt-4 rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 transition"
            >
              Xem tất cả công thức
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {recipes.map((recipe) => (
              <Link
                key={recipe.id}
                href={`/recipes/${recipe.slug}`}
                className="group flex flex-col overflow-hidden rounded-2xl border border-border bg-surface shadow-sm hover:shadow-md hover:border-brand-300 transition"
              >
                {/* Ảnh đại diện */}
                <div className="relative h-44 w-full bg-surface-muted overflow-hidden">
                  {recipe.thumbnailUrl ? (
                    <Image
                      src={recipe.thumbnailUrl}
                      alt={recipe.title}
                      fill
                      unoptimized
                      className="object-cover group-hover:scale-105 transition duration-300"
                    />
                  ) : (
                    <div className="flex h-full w-full items-center justify-center text-ink-muted text-xs">
                      Chưa có hình ảnh
                    </div>
                  )}
                  <span className="absolute top-3 right-3 rounded-full bg-black/60 px-2.5 py-0.5 text-xs font-medium text-white backdrop-blur-sm z-10">
                    {recipe.difficulty}
                  </span>
                </div>

                {/* Thông tin */}
                <div className="flex flex-1 flex-col p-4">
                  <h3 className="text-base font-bold text-ink group-hover:text-brand-700 line-clamp-1 transition">
                    {recipe.title}
                  </h3>
                  {recipe.description && (
                    <p className="mt-1 text-xs text-ink-muted line-clamp-2">{recipe.description}</p>
                  )}
                  <div className="mt-auto pt-4 flex items-center justify-between text-xs text-ink-muted border-t border-border/50">
                    <span>Chuẩn bị: {recipe.prepTime}p</span>
                    <span>Nấu: {recipe.cookTime}p</span>
                  </div>
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>

      {/* THANH PHÂN TRANG (FR-SRCH-004) */}
      {!loading && totalPages > 1 && (
        <div className="mt-10 flex items-center justify-center gap-2">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            className="rounded-lg border border-border px-3 py-1.5 text-sm font-medium text-ink hover:bg-surface-muted disabled:opacity-40 transition cursor-pointer"
          >
            ← Trang trước
          </button>
          <span className="px-4 text-sm font-medium text-ink-muted">
            Trang <strong className="text-ink font-bold">{page}</strong> / {totalPages}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            className="rounded-lg border border-border px-3 py-1.5 text-sm font-medium text-ink hover:bg-surface-muted disabled:opacity-40 transition cursor-pointer"
          >
            Trang sau →
          </button>
        </div>
      )}
    </div>
  );
}