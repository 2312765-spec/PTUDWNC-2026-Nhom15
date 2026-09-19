import React, { useState, useEffect } from 'react';
import { useApp } from '../context/AppContext';
import { 
  Layers, 
  Clock, 
  ArrowLeft, 
  Bookmark, 
  Heart, 
  ChefHat, 
  AlertCircle,
  PlusCircle,
  ChevronLeft,
  ChevronRight,
  Code2,
  ShieldCheck,
  Eye,
  SlidersHorizontal
} from 'lucide-react';
import { Category, Recipe } from '../types';
import { api, ProblemDetails } from '../services/api';

interface CategoryDetailViewProps {
  slug: string;
}

export default function CategoryDetailView({ slug }: CategoryDetailViewProps) {
  const { 
    currentUser, 
    setCurrentUser,
    navigate, 
    bookmarks, 
    toggleBookmark, 
    likedRecipeIds, 
    toggleLike 
  } = useApp();

  const [loading, setLoading] = useState(true);
  const [category, setCategory] = useState<Category | null>(null);
  const [recipes, setRecipes] = useState<any[]>([]);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 6,
    totalCount: 0,
    totalPages: 1,
  });
  const [rfcError, setRfcError] = useState<any | null>(null);
  const [showInspector, setShowInspector] = useState(false);
  const [rawResponse, setRawResponse] = useState<any>(null);

  // Fetch category & recipes from GET /api/v1/categories/:slug?page={n}&pageSize={n} (FR-CAT-002)
  const fetchCategoryData = async (targetPage = 1, targetPageSize = pagination.pageSize) => {
    setLoading(true);
    setRfcError(null);
    try {
      const data = await api.getCategoryBySlug(
        slug,
        targetPage,
        targetPageSize,
        currentUser?.role,
        currentUser?.id
      );

      setRawResponse(data);
      setCategory(data.category);
      setRecipes(data.recipes.items || []);
      setPagination({
        page: data.recipes.page,
        pageSize: data.recipes.pageSize,
        totalCount: data.recipes.totalCount,
        totalPages: data.recipes.totalPages,
      });
    } catch (err: any) {
      console.error('Lỗi khi gọi GET /api/v1/categories/:slug', err);
      setRfcError(err as ProblemDetails);
      setRawResponse(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategoryData(1, pagination.pageSize);
  }, [slug, currentUser?.role]);

  // Handle RFC 7807 404 Not Found error
  if (rfcError && rfcError.status === 404) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-16 space-y-6">
        <button
          onClick={() => navigate('/categories')}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-600 hover:text-blue-600 transition cursor-pointer"
        >
          <ArrowLeft className="w-4 h-4" />
          <span>Về danh sách danh mục</span>
        </button>

        <div className="bg-white rounded-3xl border border-rose-200 p-8 text-center space-y-4 shadow-sm">
          <div className="w-16 h-16 rounded-2xl bg-rose-50 text-rose-600 flex items-center justify-center mx-auto">
            <AlertCircle className="w-8 h-8" />
          </div>
          <h2 className="text-2xl font-extrabold text-slate-900">404 Not Found - Danh Mục Không Tồn Tại</h2>
          <p className="text-xs sm:text-sm text-slate-600 max-w-md mx-auto">
            {rfcError.detail || `Không tìm thấy danh mục với slug '${slug}'.`}
          </p>

          {/* RFC 7807 Details Box */}
          <div className="max-w-xl mx-auto text-left bg-slate-950 text-rose-300 p-4 rounded-2xl font-mono text-xs overflow-x-auto border border-slate-800">
            <p className="text-[10px] text-slate-400 mb-1">// RFC 7807 Problem Details Standard:</p>
            <pre>{JSON.stringify(rfcError, null, 2)}</pre>
          </div>

          <button
            onClick={() => navigate('/categories')}
            className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl text-xs transition cursor-pointer"
          >
            Khám phá các danh mục khác
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Back button & Role Simulator Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <button
          onClick={() => navigate('/categories')}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-600 hover:text-blue-600 transition cursor-pointer"
        >
          <ArrowLeft className="w-4 h-4" />
          <span>Tất cả danh mục (GET /api/v1/categories)</span>
        </button>

        {/* Interactive Role Switcher to demonstrate FR-CAT-002 visibility */}
        <div className="flex items-center gap-2 bg-blue-50/80 border border-blue-200/80 px-3 py-1.5 rounded-2xl text-xs">
          <span className="font-bold text-blue-900 flex items-center gap-1">
            <ShieldCheck className="w-3.5 h-3.5 text-blue-600" />
            <span>Quyền xem (FR-CAT-002):</span>
          </span>
          <div className="flex gap-1">
            <button
              onClick={() => {
                setCurrentUser(null);
                fetchCategoryData(1, pagination.pageSize);
              }}
              className={`px-2 py-0.5 rounded-lg text-[11px] font-bold transition cursor-pointer ${
                !currentUser ? 'bg-blue-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-100'
              }`}
              title="Guest chỉ thấy Published recipes"
            >
              Guest (Chỉ Published)
            </button>
            <button
              onClick={() => {
                setCurrentUser({
                  id: 'usr-chef-1',
                  displayName: 'Chef Minh Tuấn',
                  email: 'tuan@culinary.vn',
                  avatarUrl: 'https://images.unsplash.com/photo-1577219491135-ce391730fb2c?auto=format&fit=crop&w=400&q=80',
                  role: 'Author',
                  isActive: true,
                  createdAt: new Date().toISOString(),
                });
                fetchCategoryData(1, pagination.pageSize);
              }}
              className={`px-2 py-0.5 rounded-lg text-[11px] font-bold transition cursor-pointer ${
                currentUser?.role === 'Author' ? 'bg-blue-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-100'
              }`}
              title="Author thấy thêm Draft của chính mình"
            >
              Author (+ Draft cá nhân)
            </button>
            <button
              onClick={() => {
                setCurrentUser({
                  id: 'usr-admin-1',
                  displayName: 'Hoàng Nam',
                  email: 'admin@culinary.vn',
                  avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80',
                  role: 'Admin',
                  isActive: true,
                  createdAt: new Date().toISOString(),
                });
                fetchCategoryData(1, pagination.pageSize);
              }}
              className={`px-2 py-0.5 rounded-lg text-[11px] font-bold transition cursor-pointer ${
                currentUser?.role === 'Admin' ? 'bg-blue-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-100'
              }`}
              title="Admin thấy toàn bộ công thức"
            >
              Admin (Tất cả)
            </button>
          </div>
        </div>
      </div>

      {/* Category Hero Banner */}
      {category && (
        <div className="relative rounded-3xl overflow-hidden shadow-xl bg-slate-950 text-white h-64 sm:h-80 flex items-end p-6 sm:p-10 border border-slate-800">
          <img
            src={category.imageUrl}
            alt={category.name}
            className="absolute inset-0 w-full h-full object-cover opacity-35"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-slate-950 via-slate-950/70 to-transparent"></div>

          <div className="relative z-10 max-w-3xl space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="px-3 py-1 rounded-full bg-blue-600 text-white text-xs font-bold flex items-center gap-1.5 shadow-xs">
                <Layers className="w-3.5 h-3.5" />
                <span>Danh mục ẩm thực</span>
              </span>
              <span className="px-2.5 py-1 rounded-full bg-slate-800/80 text-blue-200 text-xs font-mono border border-blue-400/30">
                Slug: /{category.slug}
              </span>
            </div>

            <h1 className="text-2xl sm:text-4xl font-extrabold tracking-tight">
              {category.name}
            </h1>

            <p className="text-xs sm:text-sm text-slate-300 line-clamp-2 leading-relaxed">
              {category.description}
            </p>

            <div className="flex items-center gap-4 pt-1 text-xs text-blue-300 font-semibold">
              <span>{category.recipeCount} công thức đã xuất bản</span>
              <span>•</span>
              <button
                onClick={() => setShowInspector(!showInspector)}
                className="text-white hover:text-blue-300 underline font-mono flex items-center gap-1 cursor-pointer"
              >
                <Code2 className="w-3.5 h-3.5" />
                <span>{showInspector ? 'Ẩn RFC DTO' : 'Xem DTO (FR-CAT-002)'}</span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* API Inspector Drawer */}
      {showInspector && rawResponse && (
        <div className="bg-slate-900 rounded-2xl p-4 border border-slate-800 shadow-xl space-y-2 animate-in fade-in duration-200">
          <div className="flex items-center justify-between text-xs text-slate-400 pb-2 border-b border-slate-800">
            <span className="font-mono text-blue-400 font-bold">
              GET /api/v1/categories/{slug}?page={pagination.page}&pageSize={pagination.pageSize}
            </span>
            <span className="text-[11px] text-emerald-400 font-mono">
              200 OK • OFFSET-based SKIP: {(pagination.page - 1) * pagination.pageSize} TAKE: {pagination.pageSize}
            </span>
          </div>
          <pre className="text-[11px] text-blue-300 font-mono overflow-x-auto max-h-56 p-2 bg-slate-950 rounded-xl">
            {JSON.stringify(rawResponse, null, 2)}
          </pre>
        </div>
      )}

      {/* Recipes in this Category */}
      <div className="space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-slate-900">
              Công Thức Trong Danh Mục ({pagination.totalCount} món)
            </h2>
            <p className="text-xs text-slate-500">
              Trang {pagination.page} / {pagination.totalPages} • Hiển thị {pagination.pageSize} món mỗi trang
            </p>
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-1.5 text-xs text-slate-600">
              <span>Mỗi trang:</span>
              <select
                value={pagination.pageSize}
                onChange={(e) => fetchCategoryData(1, Number(e.target.value))}
                className="bg-white border border-slate-200 rounded-lg px-2 py-1 text-xs font-semibold focus:ring-1 focus:ring-blue-500"
              >
                <option value={3}>3 món</option>
                <option value={6}>6 món</option>
                <option value={12}>12 món</option>
              </select>
            </div>

            {currentUser && (
              <button
                onClick={() => navigate(`/dashboard/recipes/new`)}
                className="flex items-center gap-1.5 px-3.5 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-xl shadow-xs transition cursor-pointer"
              >
                <PlusCircle className="w-4 h-4" />
                <span>Đăng món mới</span>
              </button>
            )}
          </div>
        </div>

        {/* Recipes Grid */}
        {loading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-64 bg-slate-200/70 rounded-3xl animate-pulse"></div>
            ))}
          </div>
        ) : recipes.length > 0 ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {recipes.map((recipe) => (
              <div
                key={recipe.id}
                onClick={() => navigate(`/recipes/${recipe.slug}`)}
                className="group bg-white rounded-3xl overflow-hidden border border-slate-200/90 shadow-xs hover:shadow-xl hover:border-blue-300 transition-all duration-300 flex flex-col cursor-pointer"
              >
                <div className="relative h-48 w-full overflow-hidden bg-slate-100">
                  <img
                    src={recipe.thumbnailUrl || 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=800&q=80'}
                    alt={recipe.title}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                  />
                  <div className="absolute top-3 left-3 flex gap-1.5">
                    {recipe.status === 'Draft' && (
                      <span className="px-2.5 py-0.5 rounded-full text-[10px] font-extrabold bg-amber-500 text-white shadow-xs">
                        Bản Nháp (Draft)
                      </span>
                    )}
                    <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-blue-600 text-white shadow-xs">
                      {recipe.difficulty === 'Hard' ? 'Khó' : recipe.difficulty === 'Medium' ? 'Vừa' : 'Dễ'}
                    </span>
                  </div>

                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleBookmark(recipe.id);
                    }}
                    className="absolute top-3 right-3 p-2 rounded-full bg-white/90 hover:bg-white text-slate-700 shadow-xs transition cursor-pointer"
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
                    <div className="flex items-center gap-2 text-[11px] text-slate-500 mb-1.5 font-medium">
                      <span className="flex items-center gap-1 text-blue-600 font-bold">
                        <Clock className="w-3 h-3" />
                        {recipe.cookTimeMinutes} phút
                      </span>
                      <span>•</span>
                      <span>{recipe.servings} phần ăn</span>
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
                        src={recipe.authorAvatar || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80'}
                        alt={recipe.authorName}
                        className="w-5 h-5 rounded-full object-cover border border-slate-200"
                      />
                      <span className="text-[11px] text-slate-600 font-semibold truncate max-w-[110px]">
                        {recipe.authorName}
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
          <div className="bg-white rounded-3xl border border-dashed border-slate-300 p-12 text-center space-y-3">
            <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
              <ChefHat className="w-6 h-6" />
            </div>
            <h3 className="text-base font-bold text-slate-800">
              Chưa có công thức hiển thị cho vai trò {currentUser?.role || 'Guest'}
            </h3>
            <p className="text-xs text-slate-500 max-w-sm mx-auto">
              Guest chỉ thấy công thức đã xuất bản (Published). Tác giả thấy thêm các bản nháp của mình.
            </p>
          </div>
        )}

        {/* Pagination Controls (OFFSET-based) */}
        {pagination.totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 pt-6">
            <button
              disabled={pagination.page <= 1}
              onClick={() => fetchCategoryData(pagination.page - 1, pagination.pageSize)}
              className="p-2 rounded-xl border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-30 disabled:cursor-not-allowed transition cursor-pointer text-slate-700"
              title="Trang trước"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>

            {Array.from({ length: pagination.totalPages }, (_, i) => i + 1).map((p) => (
              <button
                key={p}
                onClick={() => fetchCategoryData(p, pagination.pageSize)}
                className={`w-9 h-9 rounded-xl text-xs font-bold transition cursor-pointer ${
                  pagination.page === p
                    ? 'bg-blue-600 text-white shadow-xs'
                    : 'bg-white border border-slate-200 text-slate-700 hover:bg-slate-50'
                }`}
              >
                {p}
              </button>
            ))}

            <button
              disabled={pagination.page >= pagination.totalPages}
              onClick={() => fetchCategoryData(pagination.page + 1, pagination.pageSize)}
              className="p-2 rounded-xl border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-30 disabled:cursor-not-allowed transition cursor-pointer text-slate-700"
              title="Trang sau"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
