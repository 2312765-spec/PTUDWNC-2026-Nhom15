/* eslint-disable @next/next/no-img-element */
import Link from 'next/link';
import { getCategories } from '@/lib/categories';
import { getRecipes } from '@/lib/recipes';

/**
 * Trang chủ — chủ sở hữu: B. FR-RCP-001.
 * SRS mục 5.1: ISR revalidate = 3600.
 */
export const revalidate = 3600;

export default async function HomePage() {
  // Lấy dữ liệu danh mục và công thức mới nhất (chuẩn theo getRecipes({}, 6))
  const [categoriesData, recipesData] = await Promise.all([
    getCategories().catch(() => ({ items: [] })),
    getRecipes({}, 6).catch(() => ({ items: [] })),
  ]);

  const categories = Array.isArray(categoriesData) ? categoriesData : (categoriesData?.items ?? []);
  const recipes = Array.isArray(recipesData) ? recipesData : (recipesData?.items ?? []);

  return (
    <div className="min-h-screen bg-slate-50">
      {/* 1. Hero Section & Quick Search (FR-SRCH-001) */}
      <section className="bg-gradient-to-b from-orange-50/60 to-slate-50 py-16 md:py-24 border-b border-slate-100">
        <div className="max-w-5xl mx-auto px-4 text-center">
          <span className="inline-block px-3 py-1 mb-4 text-xs font-semibold uppercase tracking-wider text-orange-600 bg-orange-100/80 rounded-full">
            Khám phá Ẩm thực Bốn phương
          </span>
          <h1 className="text-4xl md:text-5xl font-extrabold text-slate-900 tracking-tight">
            Nấu ngon mỗi ngày cùng <span className="text-orange-600">Culinary Blog</span>
          </h1>
          <p className="mt-4 text-base md:text-lg text-slate-600 max-w-2xl mx-auto">
            Hàng ngàn công thức nấu ăn chuẩn vị từ các đầu bếp gia đình và chuyên nghiệp.
          </p>

          <form action="/search" method="GET" className="mt-8 max-w-xl mx-auto flex gap-2">
            <input
              type="text"
              name="q"
              placeholder="Tìm món ăn, nguyên liệu, tác giả..."
              className="flex-1 px-4 py-3 rounded-xl border border-slate-200 bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-transparent text-slate-800 placeholder:text-slate-400"
            />
            <button
              type="submit"
              className="px-6 py-3 bg-orange-600 hover:bg-orange-700 text-white font-medium rounded-xl shadow-sm transition-colors"
            >
              Tìm kiếm
            </button>
          </form>
        </div>
      </section>

      <main className="max-w-6xl mx-auto px-4 py-12 space-y-16">
        {/* 2. Danh mục món ăn nổi bật (FR-CAT-001) */}
        <section>
          <div className="flex items-center justify-between mb-6">
            <div>
              <h2 className="text-2xl font-bold text-slate-900">Danh mục món ăn</h2>
              <p className="text-sm text-slate-500 mt-1">Khám phá theo từng phân loại hương vị</p>
            </div>
            <Link href="/categories" className="text-sm font-semibold text-orange-600 hover:text-orange-700">
              Xem tất cả →
            </Link>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-3">
            {categories.slice(0, 6).map((cat: any) => (
              <Link
                key={cat.id || cat.slug}
                href={`/categories/${cat.slug}`}
                className="group p-4 bg-white rounded-xl border border-slate-200/80 hover:border-orange-300 hover:shadow-md transition-all text-center flex flex-col items-center justify-center min-h-[100px]"
              >
                <span className="font-semibold text-slate-800 group-hover:text-orange-600 text-sm">
                  {cat.name}
                </span>
                {cat.recipeCount !== undefined && (
                  <span className="text-xs text-slate-400 mt-1">{cat.recipeCount} công thức</span>
                )}
              </Link>
            ))}
          </div>
        </section>

        {/* 3. Công thức mới nhất (FR-RCP-001) */}
        <section>
          <div className="flex items-center justify-between mb-6">
            <div>
              <h2 className="text-2xl font-bold text-slate-900">Công thức mới nhất</h2>
              <p className="text-sm text-slate-500 mt-1">Cập nhật liên tục mỗi ngày</p>
            </div>
            <Link href="/recipes" className="text-sm font-semibold text-orange-600 hover:text-orange-700">
              Khám phá thêm →
            </Link>
          </div>

          {recipes.length === 0 ? (
            <div className="text-center py-12 bg-white rounded-xl border border-dashed border-slate-200">
              <p className="text-slate-500">Chưa có công thức nào được đăng tải.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {recipes.map((recipe: any) => {
                const img = recipe.coverImageUrl || recipe.thumbnailUrl || recipe.imageUrl;
                return (
                  <Link
                    key={recipe.id || recipe.slug}
                    href={`/recipes/${recipe.slug}`}
                    className="group bg-white rounded-xl border border-slate-200/80 overflow-hidden hover:shadow-lg transition-all flex flex-col"
                  >
                    <div className="aspect-video bg-slate-100 relative overflow-hidden">
                      {img ? (
                        <img
                          src={img}
                          alt={recipe.title}
                          className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center text-slate-400 font-medium text-sm">
                          🍲 Không có ảnh
                        </div>
                      )}
                    </div>
                    <div className="p-4 flex-1 flex flex-col justify-between">
                      <div>
                        {recipe.categoryName && (
                          <span className="text-xs font-semibold text-orange-600 uppercase">
                            {recipe.categoryName}
                          </span>
                        )}
                        <h3 className="font-bold text-slate-900 group-hover:text-orange-600 mt-1 line-clamp-2">
                          {recipe.title}
                        </h3>
                        {recipe.description && (
                          <p className="text-sm text-slate-500 mt-1 line-clamp-2">
                            {recipe.description}
                          </p>
                        )}
                      </div>
                      <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-400">
                        <span>⏱ {recipe.cookTimeMinutes || recipe.cookingTimeMinutes || 30} phút</span>
                        <span>{recipe.authorName || 'Đầu bếp'}</span>
                      </div>
                    </div>
                  </Link>
                );
              })}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}