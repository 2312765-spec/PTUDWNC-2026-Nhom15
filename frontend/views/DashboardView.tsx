import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { 
  ChefHat, 
  PlusCircle, 
  CheckCircle2, 
  FileText, 
  Archive, 
  Eye, 
  Heart, 
  Edit3, 
  Trash2, 
  Layers, 
  Upload, 
  ExternalLink,
  ShieldCheck,
  Search,
  AlertTriangle,
  FolderPlus
} from 'lucide-react';
import { RecipeStatus } from '../types';
import { generateSlug } from '../data/mockData';

export default function DashboardView() {
  const { 
    currentUser, 
    recipes, 
    categories, 
    navigate, 
    publishRecipe, 
    unpublishRecipe, 
    archiveRecipe, 
    deleteRecipe,
    createCategory,
    showToast
  } = useApp();

  const [activeTab, setActiveTab] = useState<'all' | 'published' | 'draft' | 'archived'>('all');
  const [searchFilter, setSearchFilter] = useState('');
  
  // Category creation modal/form for Admin (FR-CAT-003)
  const [showNewCatModal, setShowNewCatModal] = useState(false);
  const [newCatName, setNewCatName] = useState('');
  const [newCatDesc, setNewCatDesc] = useState('');
  const [newCatImage, setNewCatImage] = useState('');

  if (!currentUser) {
    return (
      <div className="max-w-md mx-auto px-4 py-20 text-center space-y-4">
        <div className="w-16 h-16 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
          <ChefHat className="w-8 h-8" />
        </div>
        <h2 className="text-xl font-bold text-slate-900">Yêu cầu đăng nhập</h2>
        <p className="text-xs text-slate-500">
          Bạn cần đăng nhập dưới vai trò Tác giả hoặc Quản trị viên để truy cập Bảng điều khiển (FR-AUTH).
        </p>
        <button
          onClick={() => navigate('/auth/login')}
          className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-xl shadow-xs cursor-pointer"
        >
          Đăng nhập ngay
        </button>
      </div>
    );
  }

  // Filter recipes owned by user or all if Admin
  const userRecipes = recipes.filter((r) => {
    if (currentUser.role === 'Admin') return true;
    return r.authorId === currentUser.id;
  });

  const totalPublished = userRecipes.filter((r) => r.status === 'Published').length;
  const totalDraft = userRecipes.filter((r) => r.status === 'Draft').length;
  const totalArchived = userRecipes.filter((r) => r.status === 'Archived').length;
  const totalViews = userRecipes.reduce((sum, r) => sum + (r.viewCount || 0), 0);

  const displayedRecipes = userRecipes.filter((r) => {
    if (activeTab === 'published' && r.status !== 'Published') return false;
    if (activeTab === 'draft' && r.status !== 'Draft') return false;
    if (activeTab === 'archived' && r.status !== 'Archived') return false;
    if (searchFilter.trim() && !r.title.toLowerCase().includes(searchFilter.toLowerCase())) return false;
    return true;
  });

  const handleCreateCategory = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newCatName.trim() || newCatName.trim().length < 2 || newCatName.trim().length > 50) {
      showToast('Lỗi nhập liệu', 'Tên danh mục phải có độ dài từ 2 đến 50 ký tự (FR-CAT-003).', 'error');
      return;
    }
    if (/<[^>]*>/g.test(newCatName) || (newCatDesc && /<[^>]*>/g.test(newCatDesc))) {
      showToast('Lỗi bảo mật', 'Tên danh mục và mô tả không được chứa thẻ HTML.', 'error');
      return;
    }

    const res = await createCategory({
      name: newCatName.trim(),
      description: newCatDesc.trim() || undefined,
      imageUrl: newCatImage.trim() || undefined,
    });

    if (res.success) {
      setShowNewCatModal(false);
      setNewCatName('');
      setNewCatDesc('');
      setNewCatImage('');
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Top Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-6 rounded-3xl border border-slate-200/90 shadow-xs">
        <div className="flex items-center gap-4">
          <img
            src={currentUser.avatarUrl}
            alt={currentUser.displayName}
            className="w-14 h-14 rounded-2xl object-cover border-2 border-blue-500/30 shadow-xs"
          />
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl sm:text-2xl font-extrabold text-slate-900">
                {currentUser.displayName}
              </h1>
              <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-blue-100 text-blue-800">
                {currentUser.role === 'Admin' ? 'Quản Trị Viên' : 'Tác Giả (Chef)'}
              </span>
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              Bảng quản lý công thức và nội dung ẩm thực (Chương 3 SRS FR-RCP & FR-CAT)
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {currentUser.role === 'Admin' && (
            <button
              onClick={() => setShowNewCatModal(true)}
              className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold transition cursor-pointer"
            >
              <FolderPlus className="w-4 h-4 text-blue-600" />
              <span>Tạo danh mục</span>
            </button>
          )}

          <button
            onClick={() => navigate('/dashboard/recipes/new')}
            className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-md shadow-blue-500/20 transition cursor-pointer"
          >
            <PlusCircle className="w-4 h-4" />
            <span>Đăng công thức mới</span>
          </button>
        </div>
      </div>

      {/* Stats Cards (FR-OBS / Overview) */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <div className="p-4 bg-white rounded-2xl border border-blue-100 shadow-xs">
          <div className="flex items-center justify-between text-slate-500 mb-2">
            <span className="text-xs font-semibold uppercase">Tổng công thức</span>
            <FileText className="w-4 h-4 text-blue-600" />
          </div>
          <p className="text-2xl font-extrabold text-slate-900">{userRecipes.length}</p>
          <span className="text-[11px] text-blue-600 font-medium">Bao gồm nháp & đã đăng</span>
        </div>

        <div className="p-4 bg-white rounded-2xl border border-blue-100 shadow-xs">
          <div className="flex items-center justify-between text-slate-500 mb-2">
            <span className="text-xs font-semibold uppercase">Đã xuất bản</span>
            <CheckCircle2 className="w-4 h-4 text-emerald-600" />
          </div>
          <p className="text-2xl font-extrabold text-emerald-600">{totalPublished}</p>
          <span className="text-[11px] text-slate-500 font-medium">Đang hiển thị công khai</span>
        </div>

        <div className="p-4 bg-white rounded-2xl border border-blue-100 shadow-xs">
          <div className="flex items-center justify-between text-slate-500 mb-2">
            <span className="text-xs font-semibold uppercase">Bản nháp</span>
            <AlertTriangle className="w-4 h-4 text-amber-500" />
          </div>
          <p className="text-2xl font-extrabold text-amber-600">{totalDraft}</p>
          <span className="text-[11px] text-slate-500 font-medium">Chưa phát hành</span>
        </div>

        <div className="p-4 bg-white rounded-2xl border border-blue-100 shadow-xs">
          <div className="flex items-center justify-between text-slate-500 mb-2">
            <span className="text-xs font-semibold uppercase">Lượt xem</span>
            <Eye className="w-4 h-4 text-indigo-600" />
          </div>
          <p className="text-2xl font-extrabold text-indigo-600">{totalViews.toLocaleString('vi-VN')}</p>
          <span className="text-[11px] text-slate-500 font-medium">Được quan tâm cao</span>
        </div>
      </div>

      {/* Recipe Management Table Card */}
      <div className="bg-white rounded-3xl border border-slate-200/90 shadow-xs overflow-hidden">
        {/* Table Filter Tabs and Search */}
        <div className="p-4 sm:p-5 border-b border-slate-100 flex flex-col sm:flex-row gap-3 items-center justify-between">
          <div className="flex items-center gap-1.5 overflow-x-auto w-full sm:w-auto">
            {(['all', 'published', 'draft', 'archived'] as const).map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={`px-3 py-1.5 rounded-xl text-xs font-semibold capitalize transition cursor-pointer shrink-0 ${
                  activeTab === tab
                    ? 'bg-blue-600 text-white shadow-2xs'
                    : 'text-slate-600 hover:bg-slate-100'
                }`}
              >
                {tab === 'all' ? 'Tất cả' : tab === 'published' ? 'Đã xuất bản' : tab === 'draft' ? 'Bản nháp' : 'Lưu trữ'}
              </button>
            ))}
          </div>

          <div className="relative w-full sm:w-64">
            <Search className="w-3.5 h-3.5 text-slate-600 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchFilter}
              onChange={(e) => setSearchFilter(e.target.value)}
              placeholder="Lọc tên món ăn..."
              className="w-full pl-9 pr-3 py-1.5 text-xs bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>

        {/* Data Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-slate-600">
            <thead className="bg-slate-50/80 text-[11px] uppercase font-bold text-slate-500 border-b border-slate-100">
              <tr>
                <th className="py-3 px-4">Công thức</th>
                <th className="py-3 px-4">Trạng thái</th>
                <th className="py-3 px-4">Danh mục</th>
                <th className="py-3 px-4">Độ khó / Thời gian</th>
                <th className="py-3 px-4 text-center">Tương tác</th>
                <th className="py-3 px-4 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 font-medium">
              {displayedRecipes.length > 0 ? (
                displayedRecipes.map((recipe) => (
                  <tr key={recipe.id} className="hover:bg-blue-50/30 transition">
                    {/* Title & Image */}
                    <td className="py-3 px-4">
                      <div className="flex items-center gap-3">
                        <img
                          src={recipe.images[0]?.thumbnailUrl || recipe.images[0]?.originalUrl}
                          alt={recipe.title}
                          className="w-11 h-11 rounded-lg object-cover border border-slate-200"
                        />
                        <div className="min-w-0 max-w-xs">
                          <button
                            onClick={() => navigate(`/recipes/${recipe.slug}`)}
                            className="font-bold text-slate-900 hover:text-blue-600 truncate block text-left transition"
                          >
                            {recipe.title}
                          </button>
                          <span className="text-[10px] text-slate-400 block font-mono">
                            /{recipe.slug}
                          </span>
                        </div>
                      </div>
                    </td>

                    {/* Status Badge (FR-RCP-005) */}
                    <td className="py-3 px-4">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold ${
                          recipe.status === 'Published'
                            ? 'bg-emerald-100 text-emerald-800'
                            : recipe.status === 'Draft'
                            ? 'bg-amber-100 text-amber-800'
                            : 'bg-slate-100 text-slate-700'
                        }`}
                      >
                        {recipe.status === 'Published' ? 'Xuất bản' : recipe.status === 'Draft' ? 'Bản nháp' : 'Lưu trữ'}
                      </span>
                    </td>

                    {/* Category */}
                    <td className="py-3 px-4">
                      <span className="text-slate-700 font-semibold">
                        {categories.find((c) => c.id === recipe.categoryId)?.name || 'Chưa phân loại'}
                      </span>
                    </td>

                    {/* Difficulty & Time */}
                    <td className="py-3 px-4">
                      <span className="text-slate-700 font-semibold block">
                        {recipe.difficulty}
                      </span>
                      <span className="text-[10px] text-slate-400">
                        {recipe.cookTimeMinutes} phút nấu
                      </span>
                    </td>

                    {/* Stats */}
                    <td className="py-3 px-4 text-center">
                      <span className="text-slate-600 block">{recipe.viewCount} lượt xem</span>
                      <span className="text-[10px] text-rose-500 font-semibold">
                        {recipe.likeCount} thích
                      </span>
                    </td>

                    {/* Actions */}
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        {/* Publish / Unpublish action (FR-RCP-005) */}
                        {recipe.status === 'Draft' ? (
                          <button
                            onClick={() => publishRecipe(recipe.id)}
                            className="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-[11px] font-bold transition cursor-pointer"
                            title="Xuất bản công thức công khai (FR-RCP-005)"
                          >
                            Xuất bản
                          </button>
                        ) : recipe.status === 'Published' ? (
                          <button
                            onClick={() => unpublishRecipe(recipe.id)}
                            className="px-2.5 py-1 bg-amber-100 hover:bg-amber-200 text-amber-800 rounded-lg text-[11px] font-bold transition cursor-pointer"
                            title="Chuyển về bản nháp"
                          >
                            Hủy đăng
                          </button>
                        ) : null}

                        {/* Edit */}
                        <button
                          onClick={() => navigate(`/dashboard/recipes/${recipe.id}/edit`)}
                          className="p-1.5 hover:bg-slate-100 text-slate-600 hover:text-blue-600 rounded-lg transition cursor-pointer"
                          title="Chỉnh sửa công thức (FR-RCP-004)"
                        >
                          <Edit3 className="w-3.5 h-3.5" />
                        </button>

                        {/* View live */}
                        <button
                          onClick={() => navigate(`/recipes/${recipe.slug}`)}
                          className="p-1.5 hover:bg-slate-100 text-slate-600 hover:text-blue-600 rounded-lg transition cursor-pointer"
                          title="Xem trên trang công thức"
                        >
                          <ExternalLink className="w-3.5 h-3.5" />
                        </button>

                        {/* Delete */}
                        <button
                          onClick={() => {
                            if (window.confirm(`Bạn có chắc chắn muốn xóa vĩnh viễn công thức "${recipe.title}"?`)) {
                              deleteRecipe(recipe.id);
                            }
                          }}
                          className="p-1.5 hover:bg-rose-50 text-slate-400 hover:text-rose-600 rounded-lg transition cursor-pointer"
                          title="Xóa công thức (FR-RCP-007)"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-slate-400">
                    Không có công thức nào trong danh sách.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Admin Category Management Section (FR-CAT-001/003/005) */}
      {currentUser.role === 'Admin' && (
        <div className="bg-white rounded-3xl border border-slate-200/90 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 className="text-base font-bold text-slate-900 flex items-center gap-2">
                <Layers className="w-4 h-4 text-blue-600" />
                <span>Quản Lý Danh Mục Ẩm Thực (Dành cho Quản Trị Viên)</span>
              </h3>
              <p className="text-xs text-slate-500">
                Tuân thủ quy tắc FR-CAT-005: Không thể xóa danh mục đang có công thức liên kết
              </p>
            </div>
            <button
              onClick={() => setShowNewCatModal(true)}
              className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-semibold cursor-pointer"
            >
              Thêm danh mục mới
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {categories.map((c) => (
              <div
                key={c.id}
                className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex items-center justify-between"
              >
                <div>
                  <h4 className="text-xs font-bold text-slate-900">{c.name}</h4>
                  <p className="text-[11px] text-blue-700 font-mono">/{c.slug}</p>
                  <span className="text-[10px] text-slate-500">
                    {c.recipeCount} công thức liên kết
                  </span>
                </div>
                <span className="text-[10px] px-2 py-0.5 rounded bg-blue-100 text-blue-800 font-bold">
                  Thứ tự: {c.orderIndex}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* New Category Modal */}
      {showNewCatModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in">
          <form
            onSubmit={handleCreateCategory}
            className="w-full max-w-md bg-white rounded-2xl p-6 shadow-2xl border border-blue-100 space-y-4"
          >
            <h3 className="text-base font-bold text-slate-900">Tạo Danh Mục Mới (FR-CAT-003)</h3>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Tên danh mục *</label>
                <input
                  type="text"
                  required
                  value={newCatName}
                  onChange={(e) => setNewCatName(e.target.value)}
                  placeholder="Ví dụ: Món Nướng BBQ, Ăn Vặt..."
                  className="w-full p-2 border border-slate-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">Mô tả ngắn</label>
                <textarea
                  value={newCatDesc}
                  onChange={(e) => setNewCatDesc(e.target.value)}
                  rows={2}
                  placeholder="Mô tả danh mục hiển thị cho người đọc..."
                  className="w-full p-2 border border-slate-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                ></textarea>
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">URL ảnh minh họa</label>
                <input
                  type="url"
                  value={newCatImage}
                  onChange={(e) => setNewCatImage(e.target.value)}
                  placeholder="https://images.unsplash.com/..."
                  className="w-full p-2 border border-slate-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setShowNewCatModal(false)}
                className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg text-xs font-semibold cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="submit"
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-semibold cursor-pointer"
              >
                Tạo danh mục
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
