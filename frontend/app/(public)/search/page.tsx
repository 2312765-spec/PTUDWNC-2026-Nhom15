'use client';

import { useCallback, useState } from 'react';
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

export default function SearchPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);

  // Phân trang kết quả tìm kiếm
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // Hàm gọi API Search tiếng Việt (FR-SRCH-001: GET /api/v1/recipes/search?q=...)
  const handleSearch = useCallback(
    async (currentPage = 1) => {
      const query = searchTerm.trim();
      if (query.length < 2) {
        setErrorMessage('Từ khóa tìm kiếm phải có ít nhất 2 ký tự.');
        return;
      }

      setLoading(true);
      setErrorMessage(null);
      setHasSearched(true);

      try {
        const res = await apiClient.get<PagedResult<RecipeSummary>>(
          `/recipes/search?q=${encodeURIComponent(query)}&page=${currentPage}&pageSize=12`
        );
        setRecipes(res.data.items || []);
        setTotalPages(res.data.totalPages || 1);
        setTotalCount(res.data.totalCount || 0);
      } catch (err) {
        const problem = toProblemDetails(err);
        setErrorMessage(problem.detail || problem.title);
        setRecipes([]);
      } finally {
        setLoading(false);
      }
    },
    [searchTerm]
  );

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    handleSearch(1);
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    handleSearch(newPage);
  };

  return (
    <div className="mx-auto max-w-6xl px-4 py-12 sm:px-6 lg:px-8">
      {/* Tiêu đề & Ô nhập tìm kiếm */}
      <div className="text-center max-w-2xl mx-auto">
        <h1 className="text-3xl font-extrabold text-brand-700 tracking-tight sm:text-4xl">
          Tìm Kiếm Công Thức (FR-SRCH-001)
        </h1>
        <p className="mt-2 text-sm text-ink-muted">
          Hỗ trợ tìm kiếm toàn văn tiếng Việt có dấu hoặc không dấu (Ví dụ: &quot;pho&quot; ra &quot;Phở bò&quot;)
        </p>

        {/* Thanh tìm kiếm */}
        <form onSubmit={onSubmit} className="mt-6 flex gap-2">
          <input
            type="text"
            required
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Nhập tên món ăn, nguyên liệu... (tối thiểu 2 ký tự)"
            className="flex-1 rounded-xl border border-border bg-surface px-4 py-3 text-sm text-ink placeholder:text-ink-muted/60 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 shadow-sm"
          />
          <button
            type="submit"
            disabled={loading}
            className="rounded-xl bg-brand-600 px-6 py-3 text-sm font-semibold text-white shadow-sm hover:bg-brand-700 transition disabled:opacity-50"
          >
            {loading ? 'Đang tìm...' : 'Tìm kiếm'}
          </button>
        </form>

        {errorMessage && (
          <p className="mt-2 text-xs font-medium text-red-600">{errorMessage}</p>
        )}
      </div>

      {/* KẾT QUẢ TÌM KIẾM */}
      <div className="mt-12">
        {loading ? (
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {[...Array(6)].map((_, i) => (
              <div key={i} className="h-64 rounded-2xl bg-surface-muted animate-pulse border border-border" />
            ))}
          </div>
        ) : hasSearched && recipes.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-border p-12 text-center">
            <p className="text-base text-ink-muted">
              Không tìm thấy công thức nào phù hợp với từ khóa &quot;<strong>{searchTerm}</strong>&quot;.
            </p>
            <p className="mt-1 text-xs text-ink-muted">
              Thử tìm kiếm với từ khóa ngắn hơn hoặc không dấu (ví dụ: &quot;bo&quot;, &quot;ga&quot;, &quot;canh&quot;).
            </p>
          </div>
        ) : hasSearched ? (
          <>
            <div className="mb-4 text-sm text-ink-muted">
              Tìm thấy <strong className="text-ink font-semibold">{totalCount}</strong> kết quả cho từ khóa &quot;
              <span className="text-brand-700 font-medium">{searchTerm}</span>&quot;
            </div>

            <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {recipes.map((recipe) => (
                <Link
                  key={recipe.id}
                  href={`/recipes/${recipe.slug}`}
                  className="group flex flex-col overflow-hidden rounded-2xl border border-border bg-surface shadow-sm hover:shadow-md hover:border-brand-300 transition"
                >
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

                  <div className="flex flex-1 flex-col p-4">
                    <h3 className="text-base font-bold text-ink group-hover:text-brand-700 line-clamp-1 transition">
                      {recipe.title}
                    </h3>
                    {recipe.description && (
                      <p className="mt-1 text-xs text-ink-muted line-clamp-2">{recipe.description}</p>
                    )}
                    <div className="mt-auto pt-4 flex items-center justify-between text-xs text-ink-muted border-t border-border/50">
                      <span>Nấu: {recipe.cookTime}p</span>
                      <span>Chuẩn bị: {recipe.prepTime}p</span>
                    </div>
                  </div>
                </Link>
              ))}
            </div>

            {/* Phân trang kết quả tìm kiếm */}
            {totalPages > 1 && (
              <div className="mt-10 flex items-center justify-center gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => handlePageChange(Math.max(1, page - 1))}
                  className="rounded-lg border border-border px-3 py-1.5 text-sm font-medium text-ink hover:bg-surface-muted disabled:opacity-40 transition"
                >
                  ← Trang trước
                </button>
                <span className="px-4 text-sm font-medium text-ink-muted">
                  Trang <strong className="text-ink font-bold">{page}</strong> / {totalPages}
                </span>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => handlePageChange(Math.min(totalPages, page + 1))}
                  className="rounded-lg border border-border px-3 py-1.5 text-sm font-medium text-ink hover:bg-surface-muted disabled:opacity-40 transition"
                >
                  Trang sau →
                </button>
              </div>
            )}
          </>
        ) : null}
      </div>
    </div>
  );
}