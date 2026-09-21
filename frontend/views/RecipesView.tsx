import React, { useState, useMemo } from 'react';
import { useApp } from '../context/AppContext';
import { 
  Search, 
  Filter, 
  Clock, 
  ChefHat, 
  Heart, 
  Bookmark, 
  ChevronRight, 
  ChevronLeft, 
  SlidersHorizontal, 
  RotateCcw,
  Sparkles,
  Check
} from 'lucide-react';
import { RecipeDifficulty } from '../types';

export default function RecipesView() {
  const { 
    recipes, 
    categories, 
    navigate, 
    filterRecipes, 
    bookmarks, 
    toggleBookmark, 
    likedRecipeIds, 
    toggleLike,
    searchParams 
  } = useApp();

  // URL search query initial values
  const initialQuery = searchParams.get('q') || '';
  const initialCat = searchParams.get('cat') || 'all';
  const initialSaved = searchParams.get('saved') === 'true';

  const [searchQuery, setSearchQuery] = useState(initialQuery);
  const [selectedCategory, setSelectedCategory] = useState<string>(initialCat);
  const [selectedDifficulty, setSelectedDifficulty] = useState<RecipeDifficulty | 'All'>('All');
  const [maxCookTime, setMaxCookTime] = useState<number>(240);
  const [sortBy, setSortBy] = useState<'-createdAt' | 'title' | 'cookTime'>('-createdAt');
  const [onlySaved, setOnlySaved] = useState<boolean>(initialSaved);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [mobileFilterOpen, setMobileFilterOpen] = useState(false);

  const pageSize = 6;

  // Filter recipes using AppContext logic + Bookmarks filter
  const filteredList = useMemo(() => {
    let result = filterRecipes({
      searchTerm: searchQuery,
      categoryId: selectedCategory === 'all' ? undefined : selectedCategory,
      difficulty: selectedDifficulty,
      maxCookTime: maxCookTime === 240 ? undefined : maxCookTime,
      sort: sortBy,
    });

    if (onlySaved) {
      result = result.filter((r) => bookmarks.includes(r.id));
    }

    return result;
  }, [searchQuery, selectedCategory, selectedDifficulty, maxCookTime, sortBy, onlySaved, recipes, bookmarks, filterRecipes]);

  // Offset pagination (FR-SRCH-004)
  const totalItems = filteredList.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const paginatedRecipes = filteredList.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const handleResetFilters = () => {
    setSearchQuery('');
    setSelectedCategory('all');
    setSelectedDifficulty('All');
    setMaxCookTime(240);
    setSortBy('-createdAt');
    setOnlySaved(false);
    setCurrentPage(1);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-blue-900 to-blue-800 rounded-3xl p-6 sm:p-10 text-white shadow-xl relative overflow-hidden">
        <div className="max-w-2xl relative z-10 space-y-3">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-700/60 text-blue-200 text-xs font-semibold">
            <Sparkles className="w-3.5 h-3.5 text-blue-300" />
            <span>Kho Tàng Ẩm Thực Chọn Lọc</span>
          </div>
          <h1 className="text-2xl sm:text-4xl font-extrabold tracking-tight">
            Khám Phá Công Thức Nấu Ăn
          </h1>
          <p className="text-xs sm:text-sm text-blue-100/90 leading-relaxed">
            Tra cứu và lọc công thức theo danh mục, độ khó thực hiện, và thời gian chế biến với thuật toán tìm kiếm toàn văn bản tiếng Việt.
          </p>
        </div>
      </div>

      {/* Top Search & Filter Bar */}
      <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-xs flex flex-col sm:flex-row gap-3 items-center justify-between">
        <div className="relative w-full sm:w-96">
          <Search className="w-4 h-4 text-slate-600 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value);
              setCurrentPage(1);
            }}
            placeholder="Tìm theo tên món, nguyên liệu..."
            className="w-full pl-10 pr-4 py-2 text-xs sm:text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition"
          />
        </div>

        <div className="flex items-center gap-2.5 w-full sm:w-auto justify-between sm:justify-end">
          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-500 font-medium hidden sm:inline">Sắp xếp:</span>
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as '-createdAt' | 'title' | 'cookTime')}
              className="px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer"
            >
              <option value="-createdAt">Mới nhất trước</option>
              <option value="title">Tên món (A - Z)</option>
              <option value="cookTime">Thời gian nấu nhanh nhất</option>
            </select>
          </div>

          <button
            onClick={() => setMobileFilterOpen(!mobileFilterOpen)}
            className="lg:hidden flex items-center gap-1.5 px-3 py-2 bg-blue-50 text-blue-700 font-semibold rounded-xl text-xs border border-blue-200 cursor-pointer"
          >
            <SlidersHorizontal className="w-3.5 h-3.5" />
            <span>Bộ lọc</span>
          </button>
        </div>
      </div>

      {/* Main Content Layout (Sidebar Filters + Recipes Grid) */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-8 items-start">
        {/* Sidebar Filters (Desktop & Mobile Drawer) */}
        <aside
          className={`bg-white rounded-2xl border border-slate-200/90 p-5 space-y-6 shadow-xs ${
            mobileFilterOpen ? 'block' : 'hidden lg:block'
          }`}
        >
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-900 flex items-center gap-2">
              <Filter className="w-4 h-4 text-blue-600" />
              <span>Bộ lọc tìm kiếm</span>
            </h3>
            <button
              onClick={handleResetFilters}
              className="text-[11px] text-blue-600 hover:text-blue-800 font-semibold flex items-center gap-1 cursor-pointer"
            >
              <RotateCcw className="w-3 h-3" />
              <span>Đặt lại</span>
            </button>
          </div>

          {/* Bookmarks quick switch */}
          <div>
            <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 cursor-pointer">
              <input
                type="checkbox"
                checked={onlySaved}
                onChange={(e) => {
                  setOnlySaved(e.target.checked);
                  setCurrentPage(1);
                }}
                className="rounded text-blue-600 focus:ring-blue-500 w-4 h-4"
              />
              <span>Chỉ món đã lưu trong sổ tay ({bookmarks.length})</span>
            </label>
          </div>

          {/* Categories */}
          <div className="space-y-2.5">
            <h4 className="text-xs font-bold text-slate-800 uppercase tracking-wider">
              Danh mục món ăn
            </h4>
            <div className="space-y-1 text-xs">
              <button
                onClick={() => {
                  setSelectedCategory('all');
                  setCurrentPage(1);
                }}
                className={`w-full text-left px-2.5 py-1.5 rounded-lg transition flex items-center justify-between cursor-pointer ${
                  selectedCategory === 'all'
                    ? 'bg-blue-50 text-blue-700 font-bold'
                    : 'text-slate-600 hover:bg-slate-50'
                }`}
              >
                <span>Tất cả danh mục</span>
                <span className="text-[10px] text-slate-400">{recipes.length}</span>
              </button>
              {categories.map((c) => (
                <button
                  key={c.id}
                  onClick={() => {
                    setSelectedCategory(c.id);
                    setCurrentPage(1);
                  }}
                  className={`w-full text-left px-2.5 py-1.5 rounded-lg transition flex items-center justify-between cursor-pointer ${
                    selectedCategory === c.id
                      ? 'bg-blue-50 text-blue-700 font-bold'
                      : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <span className="truncate">{c.name}</span>
                  <span className="text-[10px] text-slate-400">{c.recipeCount}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Difficulty Filter */}
          <div className="space-y-2.5">
            <h4 className="text-xs font-bold text-slate-800 uppercase tracking-wider">
              Độ khó thực hiện
            </h4>
            <div className="grid grid-cols-2 gap-1.5">
              {(['All', 'Easy', 'Medium', 'Hard', 'Expert'] as const).map((diff) => (
                <button
                  key={diff}
                  onClick={() => {
                    setSelectedDifficulty(diff);
                    setCurrentPage(1);
                  }}
                  className={`px-2.5 py-1.5 rounded-lg text-xs font-semibold transition cursor-pointer border ${
                    selectedDifficulty === diff
                      ? 'bg-blue-600 text-white border-blue-600 shadow-2xs'
                      : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  {diff === 'All' ? 'Tất cả' : diff === 'Easy' ? 'Dễ' : diff === 'Medium' ? 'Vừa' : diff === 'Hard' ? 'Khó' : 'Chuyên sâu'}
                </button>
              ))}
            </div>
          </div>

          {/* Max Cook Time Slider */}
          <div className="space-y-2.5">
            <div className="flex items-center justify-between text-xs">
              <h4 className="font-bold text-slate-800 uppercase tracking-wider">
                Thời gian nấu
              </h4>
              <span className="font-bold text-blue-600">
                {maxCookTime >= 240 ? 'Tất cả' : `≤ ${maxCookTime} phút`}
              </span>
            </div>
            <input
              type="range"
              min="10"
              max="240"
              step="10"
              value={maxCookTime}
              onChange={(e) => {
                setMaxCookTime(Number(e.target.value));
                setCurrentPage(1);
              }}
              className="w-full accent-blue-600 cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-600">
              <span>10 phút</span>
              <span>60 phút</span>
              <span>120 phút</span>
              <span>4+ tiếng</span>
            </div>
          </div>
        </aside>

        {/* Recipes Grid & Results */}
        <main className="lg:col-span-3 space-y-6">
          {/* Active filter summary pill */}
          <div className="flex items-center justify-between text-xs text-slate-500">
            <p>
              Hiển thị <span className="font-bold text-slate-800">{paginatedRecipes.length}</span> trên tổng số{' '}
              <span className="font-bold text-slate-800">{totalItems}</span> công thức
            </p>
            {totalItems > 0 && (
              <span className="text-[11px] text-blue-700 bg-blue-50 px-2.5 py-0.5 rounded-full border border-blue-200">
                Trang {currentPage} / {totalPages}
              </span>
            )}
          </div>

          {/* Grid */}
          {paginatedRecipes.length > 0 ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {paginatedRecipes.map((recipe) => (
                <div
                  key={recipe.id}
                  onClick={() => navigate(`/recipes/${recipe.slug}`)}
                  className="group bg-white rounded-2xl overflow-hidden border border-slate-200/90 shadow-xs hover:shadow-xl hover:border-blue-300 transition-all duration-300 flex flex-col cursor-pointer"
                >
                  <div className="relative h-48 w-full overflow-hidden bg-slate-100">
                    <img
                      src={recipe.images[0]?.mediumUrl || recipe.images[0]?.originalUrl}
                      alt={recipe.title}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                    />
                    <div className="absolute top-3 left-3 flex gap-1.5">
                      <span className="px-2 py-0.5 rounded-md text-[11px] font-bold bg-blue-600 text-white shadow-xs">
                        {recipe.difficulty === 'Hard' ? 'Khó' : recipe.difficulty === 'Medium' ? 'Trung bình' : 'Dễ'}
                      </span>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleBookmark(recipe.id);
                      }}
                      className="absolute top-3 right-3 p-2 rounded-full bg-white/90 hover:bg-white text-slate-700 shadow-xs transition cursor-pointer"
                      title="Lưu công thức"
                    >
                      <Bookmark
                        className={`w-3.5 h-3.5 ${
                          bookmarks.includes(recipe.id) ? 'fill-blue-600 text-blue-600' : ''
                        }`}
                      />
                    </button>
                  </div>

                  <div className="p-4 flex-1 flex flex-col justify-between space-y-3">
                    <div>
                      <div className="flex items-center gap-2 text-[11px] text-slate-500 mb-1.5">
                        <span className="flex items-center gap-1">
                          <Clock className="w-3 h-3 text-blue-500" />
                          {recipe.cookTimeMinutes} phút
                        </span>
                        <span>•</span>
                        <span>{recipe.servings} phần</span>
                        <span>•</span>
                        <span>{recipe.nutrition?.calories || 0} kcal</span>
                      </div>
                      <h3 className="text-sm font-bold text-slate-900 group-hover:text-blue-600 transition-colors line-clamp-2 leading-snug">
                        {recipe.title}
                      </h3>
                      <p className="text-xs text-slate-500 line-clamp-2 mt-1">
                        {recipe.description}
                      </p>
                    </div>

                    <div className="flex items-center justify-between pt-3 border-t border-slate-100 text-xs">
                      <div className="flex items-center gap-1.5">
                        <img
                          src={recipe.author?.avatarUrl}
                          alt={recipe.author?.displayName}
                          className="w-5 h-5 rounded-full object-cover"
                        />
                        <span className="text-[11px] text-slate-600 font-medium truncate max-w-[90px]">
                          {recipe.author?.displayName}
                        </span>
                      </div>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          toggleLike(recipe.id);
                        }}
                        className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-rose-500 transition cursor-pointer"
                      >
                        <Heart
                          className={`w-3.5 h-3.5 ${
                            likedRecipeIds.includes(recipe.id) ? 'fill-rose-500 text-rose-500' : ''
                          }`}
                        />
                        <span>{recipe.likeCount}</span>
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="bg-white rounded-2xl border border-dashed border-slate-300 p-12 text-center space-y-3">
              <div className="w-12 h-12 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
                <ChefHat className="w-6 h-6" />
              </div>
              <h3 className="text-base font-bold text-slate-800">
                Không tìm thấy món ăn nào phù hợp với bộ lọc
              </h3>
              <p className="text-xs text-slate-500 max-w-sm mx-auto">
                Hãy thử nới lỏng thời gian nấu hoặc chọn tất cả danh mục để tìm thấy nhiều công thức hơn.
              </p>
              <button
                onClick={handleResetFilters}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-xl shadow-xs transition cursor-pointer"
              >
                Đặt lại toàn bộ bộ lọc
              </button>
            </div>
          )}

          {/* Pagination Navigation (FR-SRCH-004) */}
          {totalPages > 1 && (
            <div className="pt-6 border-t border-slate-200 flex items-center justify-between">
              <button
                disabled={currentPage === 1}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                className="flex items-center gap-1 px-3.5 py-2 rounded-xl text-xs font-semibold bg-white border border-slate-200 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition cursor-pointer text-slate-700"
              >
                <ChevronLeft className="w-4 h-4" />
                <span>Trang trước</span>
              </button>

              <div className="flex items-center gap-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button
                    key={page}
                    onClick={() => setCurrentPage(page)}
                    className={`w-8 h-8 rounded-lg text-xs font-bold transition cursor-pointer ${
                      currentPage === page
                        ? 'bg-blue-600 text-white shadow-xs'
                        : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-100'
                    }`}
                  >
                    {page}
                  </button>
                ))}
              </div>

              <button
                disabled={currentPage === totalPages}
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                className="flex items-center gap-1 px-3.5 py-2 rounded-xl text-xs font-semibold bg-white border border-slate-200 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition cursor-pointer text-slate-700"
              >
                <span>Trang sau</span>
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
