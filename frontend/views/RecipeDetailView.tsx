import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { 
  Clock, 
  ChefHat, 
  Users, 
  Flame, 
  Bookmark, 
  Heart, 
  Share2, 
  Printer, 
  Code2, 
  ArrowLeft, 
  Check, 
  Play, 
  Pause, 
  Edit3, 
  ShieldCheck, 
  Plus, 
  Minus,
  AlertCircle,
  ExternalLink,
  ChevronRight
} from 'lucide-react';
import { Recipe } from '../types';

interface RecipeDetailViewProps {
  slug: string;
}

export default function RecipeDetailView({ slug }: RecipeDetailViewProps) {
  const { 
    recipes, 
    currentUser, 
    navigate, 
    toggleLike, 
    likedRecipeIds, 
    toggleBookmark, 
    bookmarks,
    setJsonLdRecipe,
    startTimer,
    showToast
  } = useApp();

  const recipe = recipes.find((r) => r.slug === slug);

  // Servings portion calculator
  const initialServings = recipe?.servings || 4;
  const [servings, setServings] = useState<number>(initialServings);

  // Interactive ingredients checklist state
  const [checkedIngredients, setCheckedIngredients] = useState<Record<string, boolean>>({});

  // Active step highlighter
  const [completedSteps, setCompletedSteps] = useState<Record<string, boolean>>({});

  if (!recipe) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-20 text-center space-y-4">
        <div className="w-16 h-16 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
          <AlertCircle className="w-8 h-8" />
        </div>
        <h2 className="text-2xl font-bold text-slate-800">Không tìm thấy công thức</h2>
        <p className="text-sm text-slate-500 max-w-md mx-auto">
          Công thức bạn đang tìm có thể đã bị gỡ bỏ hoặc đường dẫn không chính xác theo chuẩn URL Slug.
        </p>
        <button
          onClick={() => navigate('/recipes')}
          className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-semibold rounded-xl text-xs sm:text-sm transition cursor-pointer"
        >
          Quay lại danh sách công thức
        </button>
      </div>
    );
  }

  // Check authorization for Draft / Archived
  const isOwner = currentUser && (currentUser.id === recipe.authorId || currentUser.role === 'Admin');
  if (recipe.status !== 'Published' && !isOwner) {
    return (
      <div className="max-w-3xl mx-auto px-4 py-20 text-center space-y-4">
        <div className="w-16 h-16 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center mx-auto">
          <ShieldCheck className="w-8 h-8" />
        </div>
        <h2 className="text-2xl font-bold text-slate-800">Công thức đang ở chế độ bản nháp (Draft)</h2>
        <p className="text-sm text-slate-500">
          Chỉ có tác giả sở hữu hoặc Quản trị viên mới có quyền xem công thức chưa xuất bản này (FR-RCP-002).
        </p>
        <button
          onClick={() => navigate('/recipes')}
          className="px-4 py-2 bg-blue-600 text-white rounded-xl text-xs font-semibold cursor-pointer"
        >
          Về trang công thức
        </button>
      </div>
    );
  }

  const isLiked = likedRecipeIds.includes(recipe.id);
  const isBookmarked = bookmarks.includes(recipe.id);
  const ratio = servings / initialServings;

  const toggleIngredientCheck = (id: string) => {
    setCheckedIngredients((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const toggleStepCompleted = (id: string) => {
    setCompletedSteps((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const handleShare = () => {
    navigator.clipboard.writeText(window.location.href);
    showToast('Đã sao chép liên kết', 'Bạn có thể chia sẻ liên kết công thức này cho bạn bè!', 'info');
  };

  const handlePrint = () => {
    window.print();
  };

  // Related recipes in same category
  const relatedRecipes = recipes
    .filter((r) => r.categoryId === recipe.categoryId && r.id !== recipe.id && r.status === 'Published')
    .slice(0, 3);

  return (
    <article className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Breadcrumbs */}
      <nav className="flex items-center gap-2 text-xs text-slate-500">
        <button onClick={() => navigate('/')} className="hover:text-blue-600 transition cursor-pointer">
          Trang chủ
        </button>
        <ChevronRight className="w-3 h-3 text-slate-400" />
        <button onClick={() => navigate('/recipes')} className="hover:text-blue-600 transition cursor-pointer">
          Công thức
        </button>
        <ChevronRight className="w-3 h-3 text-slate-400" />
        <span className="text-slate-800 font-medium truncate max-w-[200px] sm:max-w-none">
          {recipe.title}
        </span>
      </nav>

      {/* Header Info */}
      <div className="space-y-4 max-w-4xl">
        <div className="flex flex-wrap items-center gap-2">
          {recipe.status !== 'Published' && (
            <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-amber-100 text-amber-800 border border-amber-300">
              Trạng thái: {recipe.status === 'Draft' ? 'Bản Nháp' : 'Lưu Trữ'}
            </span>
          )}
          <span className="px-3 py-1 rounded-full text-xs font-bold bg-blue-100 text-blue-800">
            {recipe.difficulty === 'Hard' ? 'Độ khó: Khó' : recipe.difficulty === 'Medium' ? 'Độ khó: Vừa' : 'Độ khó: Dễ'}
          </span>
          <span className="text-xs text-slate-500 font-medium">
            Xuất bản: {new Date(recipe.publishedAt || recipe.createdAt).toLocaleDateString('vi-VN')}
          </span>
        </div>

        <h1 className="text-2xl sm:text-4xl lg:text-5xl font-extrabold text-slate-900 tracking-tight leading-tight">
          {recipe.title}
        </h1>

        <p className="text-sm sm:text-base text-slate-600 leading-relaxed">
          {recipe.description}
        </p>

        {/* Author & Action Bar */}
        <div className="flex flex-wrap items-center justify-between gap-4 pt-4 border-t border-slate-200">
          <div className="flex items-center gap-3">
            <img
              src={recipe.author?.avatarUrl}
              alt={recipe.author?.displayName}
              className="w-11 h-11 rounded-full object-cover border-2 border-blue-500/20 shadow-xs"
            />
            <div>
              <p className="text-sm font-bold text-slate-900">
                {recipe.author?.displayName}
              </p>
              <p className="text-xs text-slate-500">
                {recipe.author?.bio || 'Chuyên gia ẩm thực & tác giả công thức'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 flex-wrap">
            <button
              onClick={() => toggleLike(recipe.id)}
              className={`flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold border transition cursor-pointer ${
                isLiked
                  ? 'bg-rose-50 border-rose-200 text-rose-600 shadow-2xs'
                  : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
            >
              <Heart className={`w-4 h-4 ${isLiked ? 'fill-rose-500 text-rose-500' : ''}`} />
              <span>{recipe.likeCount}</span>
            </button>

            <button
              onClick={() => toggleBookmark(recipe.id)}
              className={`flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold border transition cursor-pointer ${
                isBookmarked
                  ? 'bg-blue-50 border-blue-200 text-blue-600 shadow-2xs'
                  : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
            >
              <Bookmark className={`w-4 h-4 ${isBookmarked ? 'fill-blue-600 text-blue-600' : ''}`} />
              <span>{isBookmarked ? 'Đã lưu' : 'Lưu'}</span>
            </button>

            <button
              onClick={handleShare}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold bg-white border border-slate-200 text-slate-700 hover:bg-slate-50 transition cursor-pointer"
              title="Chia sẻ liên kết"
            >
              <Share2 className="w-4 h-4 text-slate-500" />
              <span>Chia sẻ</span>
            </button>

            <button
              onClick={handlePrint}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold bg-white border border-slate-200 text-slate-700 hover:bg-slate-50 transition cursor-pointer"
              title="In công thức"
            >
              <Printer className="w-4 h-4 text-slate-500" />
              <span>In</span>
            </button>

            <button
              onClick={() => setJsonLdRecipe(recipe)}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold bg-blue-600 hover:bg-blue-700 text-white shadow-xs transition cursor-pointer"
              title="Xem Schema.org JSON-LD (NFR-SEO-001)"
            >
              <Code2 className="w-4 h-4" />
              <span>JSON-LD SEO</span>
            </button>

            {isOwner && (
              <button
                onClick={() => navigate(`/dashboard/recipes/${recipe.id}/edit`)}
                className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold bg-slate-800 hover:bg-slate-900 text-white shadow-xs transition cursor-pointer"
              >
                <Edit3 className="w-4 h-4" />
                <span>Chỉnh sửa</span>
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Main Cover Image */}
      <div className="relative h-72 sm:h-[420px] rounded-3xl overflow-hidden shadow-xl border border-slate-200/80 bg-slate-100">
        <img
          src={recipe.images[0]?.originalUrl}
          alt={recipe.title}
          className="w-full h-full object-cover"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-slate-950/40 via-transparent to-transparent"></div>
        {recipe.images[0]?.altText && (
          <div className="absolute bottom-3 left-4 text-white text-xs bg-slate-950/60 px-3 py-1 rounded-lg backdrop-blur-xs">
            {recipe.images[0].altText}
          </div>
        )}
      </div>

      {/* Key Metrics Quick Bar */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-white p-4 sm:p-6 rounded-2xl border border-blue-100 shadow-xs text-center">
        <div className="p-3 bg-blue-50/50 rounded-xl border border-blue-100/50">
          <Clock className="w-5 h-5 text-blue-600 mx-auto mb-1" />
          <p className="text-[11px] font-semibold text-slate-500 uppercase">Chuẩn bị</p>
          <p className="text-base font-extrabold text-slate-900">{recipe.prepTimeMinutes} phút</p>
        </div>
        <div className="p-3 bg-blue-50/50 rounded-xl border border-blue-100/50">
          <Flame className="w-5 h-5 text-amber-500 mx-auto mb-1" />
          <p className="text-[11px] font-semibold text-slate-500 uppercase">Nấu chín</p>
          <p className="text-base font-extrabold text-slate-900">{recipe.cookTimeMinutes} phút</p>
        </div>
        <div className="p-3 bg-blue-50/50 rounded-xl border border-blue-100/50">
          <Users className="w-5 h-5 text-indigo-500 mx-auto mb-1" />
          <p className="text-[11px] font-semibold text-slate-500 uppercase">Khẩu phần</p>
          <div className="flex items-center justify-center gap-2 mt-0.5">
            <button
              onClick={() => setServings((s) => Math.max(1, s - 1))}
              className="w-5 h-5 rounded bg-white hover:bg-slate-100 text-slate-700 flex items-center justify-center border border-slate-200 cursor-pointer"
            >
              <Minus className="w-3 h-3" />
            </button>
            <span className="text-base font-extrabold text-slate-900 font-mono">{servings}</span>
            <button
              onClick={() => setServings((s) => s + 1)}
              className="w-5 h-5 rounded bg-white hover:bg-slate-100 text-slate-700 flex items-center justify-center border border-slate-200 cursor-pointer"
            >
              <Plus className="w-3 h-3" />
            </button>
          </div>
        </div>
        <div className="p-3 bg-blue-50/50 rounded-xl border border-blue-100/50">
          <ChefHat className="w-5 h-5 text-emerald-500 mx-auto mb-1" />
          <p className="text-[11px] font-semibold text-slate-500 uppercase">Năng lượng</p>
          <p className="text-base font-extrabold text-slate-900">
            {Math.round((recipe.nutrition.calories || 350) * ratio)} kcal
          </p>
        </div>
      </div>

      {/* Main Content Layout: Ingredients + Instructions + Nutrition */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        {/* Left Column: Ingredients (4 cols) */}
        <div className="lg:col-span-5 space-y-6">
          <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-xs space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
                <span>Nguyên liệu</span>
                <span className="text-xs font-normal text-blue-600 bg-blue-50 px-2 py-0.5 rounded-full">
                  {recipe.ingredients.length} loại
                </span>
              </h2>
              <span className="text-xs text-slate-500 font-medium">
                Tự động nhân tỉ lệ ({servings} phần)
              </span>
            </div>

            <p className="text-xs text-slate-500 italic">
              Đánh dấu các nguyên liệu bạn đã chuẩn bị sẵn sàng:
            </p>

            <ul className="space-y-2.5">
              {recipe.ingredients.map((ing) => {
                const isChecked = !!checkedIngredients[ing.id];
                const calculatedQty = ing.quantity ? Math.round(ing.quantity * ratio * 10) / 10 : null;

                return (
                  <li
                    key={ing.id}
                    onClick={() => toggleIngredientCheck(ing.id)}
                    className={`flex items-start gap-3 p-2.5 rounded-xl transition cursor-pointer border ${
                      isChecked
                        ? 'bg-slate-50 border-slate-200 text-slate-400'
                        : 'bg-white border-slate-100 hover:bg-blue-50/40 text-slate-800'
                    }`}
                  >
                    <div
                      className={`w-5 h-5 rounded-md flex items-center justify-center shrink-0 mt-0.5 transition ${
                        isChecked
                          ? 'bg-emerald-600 text-white'
                          : 'border border-slate-300 bg-white'
                      }`}
                    >
                      {isChecked && <Check className="w-3.5 h-3.5 stroke-[3]" />}
                    </div>
                    <div className="flex-1 min-w-0">
                      <span className={`text-xs sm:text-sm font-semibold ${isChecked ? 'line-through' : ''}`}>
                        {ing.name}
                      </span>
                      {ing.notes && (
                        <span className="text-[11px] text-slate-500 block">
                          ({ing.notes})
                        </span>
                      )}
                    </div>
                    {calculatedQty && (
                      <span className="text-xs font-mono font-bold text-blue-700 shrink-0 bg-blue-50 px-2 py-0.5 rounded">
                        {calculatedQty} {ing.unit}
                      </span>
                    )}
                  </li>
                );
              })}
            </ul>
          </div>

          {/* Nutrition Facts Panel (FR-RCP-002 & Chapter 7.2.1) */}
          <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-xs space-y-4">
            <h3 className="text-base font-bold text-slate-900 border-b border-slate-100 pb-2">
              Thông tin dinh dưỡng (Mỗi khẩu phần)
            </h3>
            <div className="space-y-3 text-xs">
              <div>
                <div className="flex justify-between font-semibold text-slate-700 mb-1">
                  <span>Calories</span>
                  <span className="font-mono text-blue-700 font-bold">{recipe.nutrition.calories || 0} kcal</span>
                </div>
                <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden">
                  <div className="bg-blue-600 h-full rounded-full" style={{ width: `${Math.min(100, ((recipe.nutrition.calories || 0) / 800) * 100)}%` }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between font-semibold text-slate-700 mb-1">
                  <span>Chất đạm (Protein)</span>
                  <span className="font-mono text-blue-700 font-bold">{recipe.nutrition.protein || 0} g</span>
                </div>
                <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden">
                  <div className="bg-indigo-500 h-full rounded-full" style={{ width: `${Math.min(100, ((recipe.nutrition.protein || 0) / 60) * 100)}%` }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between font-semibold text-slate-700 mb-1">
                  <span>Tinh bột (Carbohydrates)</span>
                  <span className="font-mono text-blue-700 font-bold">{recipe.nutrition.carbohydrates || 0} g</span>
                </div>
                <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden">
                  <div className="bg-sky-500 h-full rounded-full" style={{ width: `${Math.min(100, ((recipe.nutrition.carbohydrates || 0) / 100) * 100)}%` }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between font-semibold text-slate-700 mb-1">
                  <span>Chất béo (Fat)</span>
                  <span className="font-mono text-blue-700 font-bold">{recipe.nutrition.fat || 0} g</span>
                </div>
                <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden">
                  <div className="bg-amber-500 h-full rounded-full" style={{ width: `${Math.min(100, ((recipe.nutrition.fat || 0) / 40) * 100)}%` }}></div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Right Column: Step-by-Step Instructions (7 cols) */}
        <div className="lg:col-span-7 space-y-6">
          <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-xs space-y-6">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h2 className="text-lg font-bold text-slate-900">
                Các bước thực hiện ({recipe.steps.length} bước)
              </h2>
              <span className="text-xs text-blue-600 font-medium">
                Tích hợp đồng hồ hẹn giờ nhà bếp
              </span>
            </div>

            <div className="space-y-6">
              {recipe.steps.map((step) => {
                const isStepDone = !!completedSteps[step.id];

                return (
                  <div
                    key={step.id}
                    className={`p-4 rounded-2xl border transition-all duration-200 space-y-3 ${
                      isStepDone
                        ? 'bg-slate-50/80 border-slate-200 opacity-80'
                        : 'bg-white border-slate-200 hover:border-blue-300 hover:shadow-xs'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <span className="w-7 h-7 rounded-xl bg-blue-600 text-white font-bold text-xs flex items-center justify-center shadow-xs">
                          {step.stepNumber}
                        </span>
                        <h3 className="text-sm font-bold text-slate-900">
                          {step.title}
                        </h3>
                      </div>

                      <button
                        onClick={() => toggleStepCompleted(step.id)}
                        className={`text-xs px-2.5 py-1 rounded-lg font-semibold flex items-center gap-1 transition cursor-pointer ${
                          isStepDone
                            ? 'bg-emerald-100 text-emerald-800'
                            : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                        }`}
                      >
                        <Check className="w-3.5 h-3.5" />
                        <span>{isStepDone ? 'Đã xong' : 'Đánh dấu'}</span>
                      </button>
                    </div>

                    <p className="text-xs sm:text-sm text-slate-700 leading-relaxed">
                      {step.description}
                    </p>

                    {step.imageUrl && (
                      <div className="h-44 rounded-xl overflow-hidden border border-slate-200">
                        <img
                          src={step.imageUrl}
                          alt={step.title}
                          className="w-full h-full object-cover"
                        />
                      </div>
                    )}

                    {/* Timer trigger for this step */}
                    {step.timerMinutes && (
                      <div className="pt-2 flex items-center justify-between bg-amber-50/70 p-2.5 rounded-xl border border-amber-200/70">
                        <div className="flex items-center gap-2 text-xs font-semibold text-amber-900">
                          <Clock className="w-4 h-4 text-amber-600" />
                          <span>Thời gian khuyến nghị: {step.timerMinutes} phút</span>
                        </div>
                        <button
                          onClick={() => startTimer(recipe.title, step.stepNumber, step.timerMinutes || 5)}
                          className="flex items-center gap-1.5 px-3 py-1 bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold rounded-lg shadow-xs transition cursor-pointer"
                        >
                          <Play className="w-3 h-3 fill-white" />
                          <span>Hẹn giờ ngay</span>
                        </button>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </div>

      {/* Related Recipes in Same Category */}
      {relatedRecipes.length > 0 && (
        <div className="pt-8 border-t border-slate-200 space-y-6">
          <div className="flex items-center justify-between">
            <h3 className="text-xl font-bold text-slate-900">
              Công thức cùng danh mục
            </h3>
            <button
              onClick={() => navigate(`/categories/${recipe.category?.slug}`)}
              className="text-xs font-semibold text-blue-600 hover:text-blue-700 cursor-pointer"
            >
              Xem thêm trong danh mục
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-6">
            {relatedRecipes.map((rel) => (
              <div
                key={rel.id}
                onClick={() => navigate(`/recipes/${rel.slug}`)}
                className="group bg-white rounded-2xl overflow-hidden border border-slate-200 shadow-xs hover:shadow-lg transition cursor-pointer"
              >
                <div className="h-40 overflow-hidden bg-slate-100">
                  <img
                    src={rel.images[0]?.mediumUrl || rel.images[0]?.originalUrl}
                    alt={rel.title}
                    className="w-full h-full object-cover group-hover:scale-105 transition duration-300"
                  />
                </div>
                <div className="p-3.5 space-y-1.5">
                  <span className="text-[11px] text-blue-600 font-bold">{rel.difficulty}</span>
                  <h4 className="text-xs sm:text-sm font-bold text-slate-900 group-hover:text-blue-600 truncate transition">
                    {rel.title}
                  </h4>
                  <p className="text-[11px] text-slate-500 truncate">{rel.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </article>
  );
}
