import React from 'react';
import { useApp } from '../context/AppContext';
import { Bookmark, Clock, ChefHat, Heart, ArrowRight } from 'lucide-react';

export default function BookmarksView() {
  const { recipes, bookmarks, toggleBookmark, likedRecipeIds, toggleLike, navigate } = useApp();

  const bookmarkedRecipes = recipes.filter((r) => bookmarks.includes(r.id));

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Header */}
      <div className="bg-white rounded-3xl border border-slate-200/90 p-6 sm:p-8 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 text-xs font-bold mb-2">
            <Bookmark className="w-3.5 h-3.5 fill-blue-700" />
            <span>Bộ Sưu Tập Đã Lưu</span>
          </div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900">
            Công Thức Nấu Ăn Yêu Thích
          </h1>
          <p className="text-xs sm:text-sm text-slate-500 mt-1">
            Danh sách các món ăn bạn đã lưu lại để tham khảo khi nấu nướng ({bookmarkedRecipes.length} món)
          </p>
        </div>

        <button
          onClick={() => navigate('/recipes')}
          className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl shadow-xs transition cursor-pointer"
        >
          <span>Khám phá thêm công thức</span>
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>

      {/* Grid */}
      {bookmarkedRecipes.length > 0 ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {bookmarkedRecipes.map((recipe) => (
            <div
              key={recipe.id}
              onClick={() => navigate(`/recipes/${recipe.slug}`)}
              className="group bg-white rounded-3xl overflow-hidden border border-slate-200/90 shadow-xs hover:shadow-xl hover:border-blue-300 transition-all duration-300 flex flex-col cursor-pointer"
            >
              <div className="relative h-52 w-full overflow-hidden bg-slate-100">
                <img
                  src={recipe.images[0]?.mediumUrl || recipe.images[0]?.originalUrl}
                  alt={recipe.title}
                  className="w-full h-full object-cover group-hover:scale-105 transition duration-300"
                />
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    toggleBookmark(recipe.id);
                  }}
                  className="absolute top-3 right-3 p-2 rounded-full bg-white/90 hover:bg-white text-blue-600 shadow-md transition cursor-pointer"
                  title="Bỏ lưu khỏi danh sách"
                >
                  <Bookmark className="w-4 h-4 fill-blue-600" />
                </button>
              </div>

              <div className="p-5 flex-1 flex flex-col justify-between space-y-3">
                <div>
                  <div className="flex items-center gap-2 text-xs text-slate-500 mb-2">
                    <span className="flex items-center gap-1 font-semibold text-blue-600">
                      <Clock className="w-3.5 h-3.5" />
                      {recipe.cookTimeMinutes} phút
                    </span>
                    <span>•</span>
                    <span>{recipe.servings} phần ăn</span>
                  </div>
                  <h3 className="text-base font-bold text-slate-900 group-hover:text-blue-600 transition line-clamp-2 leading-snug">
                    {recipe.title}
                  </h3>
                  <p className="text-xs text-slate-500 line-clamp-2 mt-1">
                    {recipe.description}
                  </p>
                </div>

                <div className="pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
                  <span className="text-[11px] font-semibold text-slate-600">
                    Bởi {recipe.author?.displayName}
                  </span>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleLike(recipe.id);
                    }}
                    className="flex items-center gap-1 text-slate-500 hover:text-rose-500 transition cursor-pointer"
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
        <div className="bg-white rounded-3xl border border-dashed border-slate-300 p-16 text-center space-y-4">
          <div className="w-14 h-14 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
            <Bookmark className="w-7 h-7" />
          </div>
          <h3 className="text-lg font-bold text-slate-800">
            Bạn chưa lưu công thức nào
          </h3>
          <p className="text-xs text-slate-500 max-w-sm mx-auto">
            Nhấn vào biểu tượng Bookmark trên bất kỳ công thức nào để lưu lại và xem lại tại đây!
          </p>
          <button
            onClick={() => navigate('/recipes')}
            className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl text-xs shadow-xs transition cursor-pointer"
          >
            Duyệt công thức ngay
          </button>
        </div>
      )}
    </div>
  );
}
