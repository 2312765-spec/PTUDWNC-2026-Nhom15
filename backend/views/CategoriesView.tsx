import React, { useState, useEffect } from 'react';
import { useApp } from '../context/AppContext';
import { 
  Layers, 
  ArrowRight, 
  Database, 
  RefreshCw, 
  Plus, 
  Clock, 
  ShieldAlert, 
  CheckCircle2, 
  Code2, 
  Zap, 
  Sparkles,
  AlertTriangle,
  FileCode2,
  Activity
} from 'lucide-react';
import { Category } from '../types';
import DotnetArchitectureModal from '../components/DotnetArchitectureModal';
import { api, ProblemDetails } from '../services/api';

export default function CategoriesView() {
  const { currentUser, navigate, showToast, setApiActivityModalOpen } = useApp();

  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [cacheHeader, setCacheHeader] = useState<{ status: 'HIT' | 'MISS' | 'NONE'; ttl: string }>({
    status: 'NONE',
    ttl: '',
  });
  const [rawApiResponse, setRawApiResponse] = useState<any>(null);
  const [showApiInspector, setShowApiInspector] = useState(false);
  const [showDotnetModal, setShowDotnetModal] = useState(false);

  // Admin Create Category Modal State (FR-CAT-003)
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [catName, setCatName] = useState('');
  const [catDesc, setCatDesc] = useState('');
  const [catImage, setCatImage] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [createdLocation, setCreatedLocation] = useState<string | null>(null);

  // Fetch categories from GET /api/v1/categories (FR-CAT-001)
  const fetchCategories = async (forceRefresh = false) => {
    setLoading(true);
    try {
      const res = await api.getCategories(forceRefresh);
      setCacheHeader({
        status: res.cacheStatus,
        ttl: res.ttlRemaining || '3600s',
      });
      setRawApiResponse(res.data);
      if (Array.isArray(res.data)) {
        setCategories(res.data);
      }
    } catch (err: any) {
      console.error('Lỗi tải danh mục:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategories();
  }, []);

  // Admin Create Category (FR-CAT-003)
  const handleCreateCategory = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    setCreatedLocation(null);

    // Validation (Client-side & Server-side): 2-50 chars, no HTML
    if (catName.trim().length < 2 || catName.trim().length > 50) {
      setFormError('Tên danh mục phải có độ dài từ 2 đến 50 ký tự (FR-CAT-003).');
      return;
    }

    if (/<[^>]*>/g.test(catName) || (catDesc && /<[^>]*>/g.test(catDesc))) {
      setFormError('Tên danh mục hoặc mô tả không được chứa mã HTML.');
      return;
    }

    setSubmitting(true);
    try {
      const res = await api.createCategory(
        {
          name: catName.trim(),
          description: catDesc.trim() || undefined,
          imageUrl: catImage.trim() || undefined,
        },
        currentUser?.role === 'Admin' ? 'Admin' : 'Guest',
        currentUser?.id || 'guest'
      );

      setCreatedLocation(res.location);
      showToast(
        'Tạo danh mục thành công (201 Created)',
        `Slug: ${res.category.slug} • Cache đã được invalidate tự động!`,
        'success'
      );

      // Refresh categories from API
      await fetchCategories();
      setTimeout(() => {
        setShowCreateModal(false);
        setCatName('');
        setCatDesc('');
        setCatImage('');
        setCreatedLocation(null);
      }, 1200);
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      setFormError(pErr.detail || pErr.title || 'Đã xảy ra lỗi khi tạo danh mục.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-blue-900 via-blue-800 to-indigo-900 rounded-3xl p-6 sm:p-10 text-white shadow-xl relative overflow-hidden">
        <div className="max-w-3xl relative z-10 space-y-3">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-700/60 text-blue-200 text-xs font-semibold backdrop-blur-xs">
            <Layers className="w-3.5 h-3.5 text-blue-300" />
            <span>Phân Loại Danh Mục Ẩm Thực (Module FR-CAT)</span>
          </div>

          <h1 className="text-2xl sm:text-4xl font-extrabold tracking-tight">
            Danh Mục Công Thức Nấu Ăn
          </h1>

          <p className="text-xs sm:text-sm text-blue-100/90 leading-relaxed">
            Danh mục được tạo và duy trì bởi Quản trị viên (Admin). Sắp xếp theo tên tăng dần, tích hợp bộ nhớ đệm <strong className="text-white">IMemoryCache (TTL 60 phút)</strong> và tự động đếm số lượng món ăn đã xuất bản.
          </p>

          {/* Real-time Cache Status & Controls */}
          <div className="pt-2 flex flex-wrap items-center gap-3">
            <div className="flex items-center gap-2 bg-blue-950/70 border border-blue-400/30 px-3 py-1.5 rounded-xl text-xs backdrop-blur-sm">
              <Database className="w-3.5 h-3.5 text-blue-300" />
              <span className="text-blue-200 font-mono">IMemoryCache:</span>
              <span
                className={`px-2 py-0.5 rounded-md font-extrabold text-[11px] ${
                  cacheHeader.status === 'HIT'
                    ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                    : 'bg-amber-500/20 text-amber-300 border border-amber-500/40'
                }`}
              >
                {cacheHeader.status === 'HIT' ? 'CACHE HIT' : 'CACHE MISS'}
              </span>
              <span className="text-[11px] text-blue-300">
                (Sliding TTL: {cacheHeader.ttl})
              </span>
            </div>

            <button
              onClick={() => fetchCategories(false)}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-800/80 hover:bg-blue-700 text-white text-xs font-semibold rounded-xl border border-blue-600 transition cursor-pointer"
              title="Gửi request GET /api/v1/categories (Kiểm tra Cache Hit)"
            >
              <RefreshCw className={`w-3 h-3 ${loading ? 'animate-spin' : ''}`} />
              <span>Gọi lại API (Test Cache)</span>
            </button>

            <button
              onClick={() => fetchCategories(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-rose-900/60 hover:bg-rose-800/80 text-rose-200 text-xs font-semibold rounded-xl border border-rose-500/40 transition cursor-pointer"
              title="Xóa cache categories:all và gọi lại (Test Cache Miss)"
            >
              <Zap className="w-3 h-3 text-amber-400" />
              <span>Xóa Cache & Nạp lại</span>
            </button>

            <button
              onClick={() => setShowApiInspector(!showApiInspector)}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-white/10 hover:bg-white/20 text-white text-xs font-semibold rounded-xl border border-white/20 transition cursor-pointer"
            >
              <Code2 className="w-3.5 h-3.5" />
              <span>{showApiInspector ? 'Ẩn JSON DTO' : 'Xem JSON DTO'}</span>
            </button>

            <button
              onClick={() => setShowDotnetModal(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-900/30 transition cursor-pointer ml-auto"
              title="Xem mã nguồn ASP.NET Core .NET 10 (Clean Architecture & MediatR)"
            >
              <FileCode2 className="w-3.5 h-3.5 text-purple-200" />
              <span>Kiến Trúc & Code .NET 10</span>
            </button>
          </div>
        </div>
      </div>

      {/* JSON DTO Inspector for Developers / Evaluators */}
      {showApiInspector && rawApiResponse && (
        <div className="bg-slate-900 rounded-2xl p-4 border border-slate-800 shadow-xl space-y-2 animate-in fade-in duration-200">
          <div className="flex items-center justify-between text-xs text-slate-400 pb-2 border-b border-slate-800">
            <span className="font-mono text-blue-400 font-bold">
              GET /api/v1/categories → Response Body (CategoryDto[])
            </span>
            <span className="text-[11px] text-emerald-400 font-mono">
              Status: 200 OK • X-Cache: {cacheHeader.status}
            </span>
          </div>
          <pre className="text-[11px] text-blue-300 font-mono overflow-x-auto max-h-56 p-2 bg-slate-950 rounded-xl">
            {JSON.stringify(rawApiResponse, null, 2)}
          </pre>
        </div>
      )}

      {/* Actions Bar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-lg sm:text-xl font-extrabold text-slate-900">
            Tất Cả Danh Mục ({categories.length})
          </h2>
          <p className="text-xs text-slate-500">
            Tự động sắp xếp Name tăng dần (A-Z) theo đặc tả FR-CAT-001
          </p>
        </div>

        {/* FR-CAT-003: Button to Create Category [Admin] */}
        <div className="flex items-center gap-2">
          {currentUser?.role !== 'Admin' ? (
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs font-medium">
              <ShieldAlert className="w-4 h-4 text-amber-600" />
              <span>Đang là {currentUser ? currentUser.role : 'Guest'}. Đăng nhập Admin để tạo danh mục (FR-CAT-003)</span>
              <button
                onClick={() => navigate('/auth/login')}
                className="underline font-bold hover:text-amber-900 cursor-pointer"
              >
                Đăng nhập
              </button>
            </div>
          ) : (
            <button
              onClick={() => setShowCreateModal(true)}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl shadow-md shadow-blue-500/20 transition cursor-pointer"
            >
              <Plus className="w-4 h-4" />
              <span>Tạo Danh Mục Mới (POST /api/v1/categories)</span>
            </button>
          )}
        </div>
      </div>

      {/* Categories Grid */}
      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-72 bg-slate-200/70 rounded-3xl animate-pulse"></div>
          ))}
        </div>
      ) : categories.length > 0 ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {categories.map((category) => (
            <div
              key={category.id}
              onClick={() => navigate(`/categories/${category.slug}`)}
              className="group bg-white rounded-3xl overflow-hidden border border-slate-200/90 shadow-xs hover:shadow-xl hover:border-blue-400 transition-all duration-300 flex flex-col cursor-pointer"
            >
              {/* Cover Image */}
              <div className="relative h-52 w-full overflow-hidden bg-slate-100">
                <img
                  src={category.imageUrl}
                  alt={category.name}
                  className="w-full h-full object-cover group-hover:scale-108 transition-transform duration-500"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-slate-950/80 via-slate-950/20 to-transparent"></div>
                <div className="absolute top-4 right-4">
                  <span className="px-3 py-1 rounded-full text-xs font-bold bg-blue-600/95 text-white shadow-md backdrop-blur-xs flex items-center gap-1.5">
                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                    {category.recipeCount || 0} món xuất bản
                  </span>
                </div>
                <div className="absolute bottom-4 left-4 right-4 text-white">
                  <span className="text-[10px] uppercase font-bold tracking-widest text-blue-300 mb-1 block">
                    Thứ tự: {category.orderIndex || 1}
                  </span>
                  <h3 className="text-xl font-bold leading-snug drop-shadow-sm group-hover:text-blue-300 transition">
                    {category.name}
                  </h3>
                </div>
              </div>

              {/* Description & Slug info */}
              <div className="p-5 flex-1 flex flex-col justify-between space-y-4">
                <p className="text-xs text-slate-600 leading-relaxed line-clamp-2">
                  {category.description}
                </p>

                <div className="pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
                  <span className="text-slate-500 font-medium">
                    Slug: <code className="text-blue-700 bg-blue-50 px-2 py-0.5 rounded font-mono text-[11px] font-bold">/{category.slug}</code>
                  </span>
                  <div className="flex items-center gap-1 font-bold text-blue-600 group-hover:text-blue-700 group-hover:translate-x-1 transition">
                    <span>Xem món (FR-CAT-002)</span>
                    <ArrowRight className="w-3.5 h-3.5" />
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="p-12 text-center bg-white rounded-3xl border border-dashed border-slate-300">
          <p className="text-sm text-slate-500">Chưa có danh mục nào trong hệ thống (A1 - Empty Array).</p>
        </div>
      )}

      {/* Admin Create Category Modal (FR-CAT-003) */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in">
          <div className="w-full max-w-lg bg-white rounded-3xl p-6 sm:p-8 shadow-2xl border border-blue-100 space-y-5 animate-in zoom-in-95">
            <div className="flex items-start justify-between pb-3 border-b border-slate-100">
              <div>
                <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-blue-100 text-blue-800 uppercase">
                  Yêu cầu FR-CAT-003 [Admin]
                </span>
                <h3 className="text-lg font-bold text-slate-900 mt-1">
                  Tạo Danh Mục Công Thức Mới
                </h3>
                <p className="text-xs text-slate-500">
                  POST /api/v1/categories • Slug tự động sinh • Invalidate IMemoryCache
                </p>
              </div>
              <button
                type="button"
                onClick={() => setShowCreateModal(false)}
                className="text-slate-400 hover:text-slate-600 text-sm font-bold p-1 cursor-pointer"
              >
                ✕
              </button>
            </div>

            {formError && (
              <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{formError}</span>
              </div>
            )}

            {createdLocation && (
              <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 shrink-0" />
                <span>Thành công! Header Location: <strong>{createdLocation}</strong></span>
              </div>
            )}

            <form onSubmit={handleCreateCategory} className="space-y-4 text-xs">
              <div>
                <label className="font-bold text-slate-800 block mb-1">
                  Tên danh mục * (2 đến 50 ký tự, không chứa HTML)
                </label>
                <input
                  type="text"
                  required
                  value={catName}
                  onChange={(e) => setCatName(e.target.value)}
                  placeholder="Ví dụ: Món Ăn Sáng, Bánh Dân Gian..."
                  className="w-full p-2.5 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 focus:outline-none"
                />
                <span className="text-[11px] text-slate-400 mt-1 block">
                  Slug sẽ được slugify tự động (bỏ dấu, chuyển thường, thay dấu cách bằng "-"). Nếu trùng hệ thống tự động thêm hậu tố số "-2", "-3"...
                </span>
              </div>

              <div>
                <label className="font-bold text-slate-800 block mb-1">
                  Mô tả danh mục (Description)
                </label>
                <textarea
                  rows={2}
                  value={catDesc}
                  onChange={(e) => setCatDesc(e.target.value)}
                  placeholder="Mô tả các món ăn trong chủ đề danh mục..."
                  className="w-full p-2.5 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 focus:outline-none"
                ></textarea>
              </div>

              <div>
                <label className="font-bold text-slate-800 block mb-1">
                  Đường dẫn ảnh minh họa (Image URL)
                </label>
                <input
                  type="url"
                  value={catImage}
                  onChange={(e) => setCatImage(e.target.value)}
                  placeholder="https://images.unsplash.com/photo-..."
                  className="w-full p-2.5 bg-slate-50 border border-slate-300 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 focus:outline-none"
                />
              </div>

              <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold rounded-xl cursor-pointer"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-md shadow-blue-500/20 flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                >
                  {submitting ? 'Đang tạo...' : 'Tạo Danh Mục (201 Created)'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ASP.NET Core .NET 10 Clean Architecture Modal */}
      <DotnetArchitectureModal 
        isOpen={showDotnetModal} 
        onClose={() => setShowDotnetModal(false)} 
      />
    </div>
  );
}
