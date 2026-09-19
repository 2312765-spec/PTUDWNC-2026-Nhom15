import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { 
  User, 
  Mail, 
  ShieldCheck, 
  ChefHat, 
  Calendar, 
  Save, 
  BookOpen, 
  Clock, 
  Heart,
  CheckCircle2,
  ExternalLink
} from 'lucide-react';

export default function ProfileView() {
  const { currentUser, setCurrentUser, recipes, navigate, showToast } = useApp();

  const [displayName, setDisplayName] = useState(currentUser?.displayName || '');
  const [avatarUrl, setAvatarUrl] = useState(currentUser?.avatarUrl || '');
  const [bio, setBio] = useState(currentUser?.bio || '');

  if (!currentUser) {
    return (
      <div className="max-w-md mx-auto px-4 py-20 text-center space-y-4">
        <h2 className="text-xl font-bold text-slate-800">Yêu cầu đăng nhập</h2>
        <p className="text-xs text-slate-500">Bạn cần đăng nhập để xem thông tin hồ sơ (FR-AUTH-006).</p>
        <button
          onClick={() => navigate('/auth/login')}
          className="px-4 py-2 bg-blue-600 text-white rounded-xl text-xs font-semibold cursor-pointer"
        >
          Đăng nhập
        </button>
      </div>
    );
  }

  const userRecipes = recipes.filter((r) => r.authorId === currentUser.id);

  const handleUpdateProfile = (e: React.FormEvent) => {
    e.preventDefault();
    if (!displayName.trim()) {
      showToast('Lỗi', 'Tên hiển thị không được để trống (FR-AUTH-007).', 'error');
      return;
    }

    setCurrentUser({
      ...currentUser,
      displayName: displayName.trim(),
      avatarUrl: avatarUrl.trim() || currentUser.avatarUrl,
      bio: bio.trim(),
    });

    showToast('Cập nhật thành công', 'Hồ sơ cá nhân đã được lưu (FR-AUTH-007).', 'success');
  };

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Header */}
      <div className="bg-white rounded-3xl border border-slate-200/90 p-6 sm:p-8 shadow-xs">
        <div className="flex flex-col sm:flex-row items-center sm:items-start gap-6">
          <img
            src={avatarUrl || currentUser.avatarUrl}
            alt={currentUser.displayName}
            className="w-24 h-24 rounded-3xl object-cover border-4 border-blue-100 shadow-md"
          />
          <div className="flex-1 text-center sm:text-left space-y-2">
            <div className="flex flex-wrap items-center justify-center sm:justify-start gap-2">
              <h1 className="text-2xl font-extrabold text-slate-900">
                {currentUser.displayName}
              </h1>
              <span className="px-2.5 py-0.5 rounded-full text-xs font-bold bg-blue-100 text-blue-800">
                {currentUser.role}
              </span>
            </div>
            <p className="text-xs text-slate-500 flex items-center justify-center sm:justify-start gap-1">
              <Mail className="w-3.5 h-3.5 text-slate-400" />
              <span>{currentUser.email}</span>
            </p>
            <p className="text-xs text-slate-600 max-w-xl leading-relaxed">
              {currentUser.bio || 'Chưa cập nhật tiểu sử tác giả.'}
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        {/* Profile Edit Form (FR-AUTH-007) */}
        <div className="lg:col-span-5 bg-white rounded-3xl border border-slate-200/90 p-6 shadow-xs space-y-4">
          <div className="pb-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-900">
              Cập nhật hồ sơ (FR-AUTH-007 PATCH /auth/me)
            </h3>
            <p className="text-[11px] text-slate-500">
              Chỉnh sửa Tên hiển thị công khai và Ảnh đại diện
            </p>
          </div>

          <form onSubmit={handleUpdateProfile} className="space-y-4 text-xs">
            <div>
              <label className="font-bold text-slate-700 block mb-1">
                Tên hiển thị (DisplayName) *
              </label>
              <input
                type="text"
                required
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                className="w-full p-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">
                Địa chỉ Email (Read-only)
              </label>
              <input
                type="email"
                disabled
                value={currentUser.email}
                className="w-full p-2.5 bg-slate-100 text-slate-500 border border-slate-200 rounded-xl cursor-not-allowed"
              />
              <span className="text-[10px] text-slate-400 mt-0.5 block">
                Thay đổi email cần quy trình xác thực mã OTP độc lập theo SRS
              </span>
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">
                Đường dẫn ảnh đại diện (Avatar URL)
              </label>
              <input
                type="url"
                value={avatarUrl}
                onChange={(e) => setAvatarUrl(e.target.value)}
                className="w-full p-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">
                Tiểu sử ngắn (Bio)
              </label>
              <textarea
                rows={3}
                value={bio}
                onChange={(e) => setBio(e.target.value)}
                placeholder="Giới thiệu về kinh nghiệm, trường phái nấu ăn của bạn..."
                className="w-full p-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
              ></textarea>
            </div>

            <button
              type="submit"
              className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs transition flex items-center justify-center gap-1.5 cursor-pointer"
            >
              <Save className="w-4 h-4" />
              <span>Lưu thay đổi hồ sơ</span>
            </button>
          </form>
        </div>

        {/* User recipes list */}
        <div className="lg:col-span-7 bg-white rounded-3xl border border-slate-200/90 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-900">
              Công thức đã đóng góp ({userRecipes.length})
            </h3>
            <button
              onClick={() => navigate('/dashboard/recipes/new')}
              className="text-xs font-semibold text-blue-600 hover:text-blue-700 cursor-pointer"
            >
              + Đăng công thức mới
            </button>
          </div>

          <div className="space-y-3">
            {userRecipes.length > 0 ? (
              userRecipes.map((r) => (
                <div
                  key={r.id}
                  onClick={() => navigate(`/recipes/${r.slug}`)}
                  className="flex items-center gap-3 p-3 rounded-2xl border border-slate-100 hover:border-blue-200 hover:bg-blue-50/40 transition cursor-pointer"
                >
                  <img
                    src={r.images[0]?.thumbnailUrl || r.images[0]?.originalUrl}
                    alt={r.title}
                    className="w-14 h-14 rounded-xl object-cover"
                  />
                  <div className="flex-1 min-w-0">
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-blue-100 text-blue-800">
                      {r.status}
                    </span>
                    <h4 className="text-xs sm:text-sm font-bold text-slate-900 truncate mt-1">
                      {r.title}
                    </h4>
                    <p className="text-[11px] text-slate-500">
                      {r.cookTimeMinutes} phút • {r.servings} phần • {r.likeCount} lượt thích
                    </p>
                  </div>
                  <ExternalLink className="w-4 h-4 text-slate-400 hover:text-blue-600" />
                </div>
              ))
            ) : (
              <p className="text-xs text-slate-400 text-center py-8">
                Bạn chưa đăng công thức nào.
              </p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
