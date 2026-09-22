import React from 'react';
import { useApp } from '../context/AppContext';
import { 
  Search, 
  Clock, 
  ChefHat, 
  Flame, 
  Sparkles, 
  ArrowRight, 
  Bookmark, 
  Heart, 
  ShieldCheck, 
  Layers, 
  Award, 
  CheckCircle2,
  TrendingUp,
  PlusCircle,
  ExternalLink
} from 'lucide-react';

export default function HomeView() {
  const { 
    recipes, 
    categories, 
    navigate, 
    setSearchModalOpen, 
    toggleLike, 
    likedRecipeIds,
    toggleBookmark,
    bookmarks,
    setJsonLdRecipe
  } = useApp();

  const publishedRecipes = recipes.filter((r) => r.status === 'Published');
  const featuredRecipe = publishedRecipes[0] || recipes[0];
  const trendingRecipes = publishedRecipes.slice(1, 5);

  const quickTags = ['Phở bò', 'Cá kho tộ', 'Canh chua', 'Gỏi cuốn', 'Bánh flan', 'Cà phê trứng'];

  return (
    <div className="space-y-16 pb-16">
      {/* Hero Section: Blue theme banner */}
      <section className="relative overflow-hidden bg-gradient-to-br from-blue-900 via-blue-800 to-indigo-950 text-white pt-16 pb-20 px-4 sm:px-6 lg:px-8">
        {/* Subtle background mesh / glows */}
        <div className="absolute inset-0 opacity-10 bg-[radial-gradient(#38bdf8_1px,transparent_1px)] [background-size:16px_16px] pointer-events-none"></div>
        <div className="absolute top-0 right-0 -mt-10 -mr-10 w-96 h-96 rounded-full bg-blue-500/20 blur-3xl pointer-events-none"></div>
        <div className="absolute bottom-0 left-10 w-80 h-80 rounded-full bg-sky-400/15 blur-3xl pointer-events-none"></div>

        <div className="max-w-7xl mx-auto relative z-10">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            {/* Left Col: Hero copy */}
            <div className="lg:col-span-7 space-y-6 text-left">
              <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-500/20 border border-blue-400/30 text-xs font-semibold text-blue-200">
                <Sparkles className="w-3.5 h-3.5 text-blue-300" />
                <span>Nền tảng Ẩm thực & Công thức Nấu ăn Chuẩn Vị</span>
              </div>

              <h1 className="text-3xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight leading-tight sm:leading-none text-white">
                Khám phá & Chia sẻ <br />
                <span className="text-transparent bg-clip-text bg-gradient-to-r from-sky-300 via-blue-200 to-white">
                  Công Thức Nấu Ăn
                </span>
              </h1>

              <p className="text-sm sm:text-base text-blue-100/90 max-w-xl font-normal leading-relaxed">
                Hơn hàng trăm công thức chi tiết từ các đầu bếp hàng đầu. Hướng dẫn từng bước với thời gian chuẩn xác, tỉ lệ dinh dưỡng khoa học và hình ảnh chân thực.
              </p>

              {/* Instant Search Bar */}
              <div className="pt-2 max-w-xl">
                <div 
                  onClick={() => setSearchModalOpen(true)}
                  className="bg-white/95 rounded-2xl p-2 shadow-2xl flex items-center gap-2 text-slate-700 cursor-pointer hover:ring-4 hover:ring-blue-400/30 transition duration-200"
                >
                  <div className="p-2.5 rounded-xl bg-blue-600 text-white">
                    <Search className="w-5 h-5" />
                  </div>
                  <span className="text-slate-400 text-sm flex-1 font-medium pl-1">
                    Tìm món canh chua, phở bò, thịt kho, đồ tráng miệng...
                  </span>
                  <button className="px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs sm:text-sm font-semibold transition cursor-pointer">
                    Tìm kiếm
                  </button>
                </div>

                {/* Popular keyword pills */}
                <div className="flex items-center gap-2 mt-3 flex-wrap text-xs text-blue-200">
                  <span className="text-blue-300/80 font-medium">Gợi ý:</span>
                  {quickTags.map((tag) => (
                    <button
                      key={tag}
                      onClick={() => navigate(`/recipes?q=${encodeURIComponent(tag)}`)}
                      className="px-2.5 py-1 rounded-full bg-blue-800/60 hover:bg-blue-700/80 text-blue-100 border border-blue-700/50 transition cursor-pointer text-[11px]"
                    >
                      {tag}
                    </button>
                  ))}
                </div>
              </div>

              {/* Architecture highlights pill */}
              <div className="pt-4 flex flex-wrap gap-4 text-xs text-blue-200/90 border-t border-blue-700/40">
                <div className="flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-sky-400" />
                  <span>Next.js App Router (ISR/SSR)</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-sky-400" />
                  <span>Schema.org Recipe JSON-LD</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-sky-400" />
                  <span>FTS Tiếng Việt không dấu</span>
                </div>
              </div>
            </div>

            {/* Right Col: Featured recipe card banner */}
            {featuredRecipe && (
              <div className="lg:col-span-5">
                <div className="relative group rounded-3xl overflow-hidden bg-white text-slate-900 shadow-2xl border border-blue-200/30 transition-all duration-300 hover:shadow-blue-500/20">
                  {/* Image */}
                  <div className="relative h-64 sm:h-72 w-full overflow-hidden">
                    <img
                      src={featuredRecipe.images[0]?.originalUrl}
                      alt={featuredRecipe.title}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                    />
                    <div className="absolute inset-0 bg-gradient-to-t from-slate-950/80 via-transparent to-transparent"></div>
                    
                    {/* Badges */}
                    <div className="absolute top-4 left-4 flex gap-2">
                      <span className="px-3 py-1 rounded-full text-xs font-bold bg-blue-600 text-white shadow-md">
                        Món Nổi Bật
                      </span>
                      <span className="px-2.5 py-1 rounded-full text-xs font-semibold bg-white/90 text-slate-800 backdrop-blur-xs">
                        {featuredRecipe.difficulty === 'Hard' ? 'Khó' : featuredRecipe.difficulty === 'Medium' ? 'Trung bình' : 'Dễ'}
                      </span>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleBookmark(featuredRecipe.id);
                      }}
                      className="absolute top-4 right-4 p-2.5 rounded-full bg-white/80 hover:bg-white text-slate-700 shadow-md backdrop-blur-xs transition cursor-pointer"
                      title="Lưu công thức"
                    >
                      <Bookmark
                        className={`w-4 h-4 ${
                          bookmarks.includes(featuredRecipe.id) ? 'fill-blue-600 text-blue-600' : ''
                        }`}
                      />
                    </button>

                    <div className="absolute bottom-4 left-4 right-4 text-white">
                      <div className="flex items-center gap-3 text-xs text-blue-200 mb-1">
                        <span className="flex items-center gap-1">
                          <Clock className="w-3.5 h-3.5" />
                          {featuredRecipe.prepTimeMinutes + featuredRecipe.cookTimeMinutes} phút
                        </span>
                        <span>•</span>
                        <span>{featuredRecipe.servings} khẩu phần</span>
                        <span>•</span>
                        <span>{featuredRecipe.ingredients.length} nguyên liệu</span>
                      </div>
                      <h3 className="text-xl font-bold leading-snug drop-shadow-sm line-clamp-1">
                        {featuredRecipe.title}
                      </h3>
                    </div>
                  </div>

                  {/* Card Content & Action */}
                  <div className="p-5 space-y-4">
                    <p className="text-xs text-slate-600 line-clamp-2">
                      {featuredRecipe.description}
                    </p>

                    <div className="flex items-center justify-between pt-2 border-t border-slate-100">
                      <div className="flex items-center gap-2">
                        <img
                          src={featuredRecipe.author?.avatarUrl}
                          alt={featuredRecipe.author?.displayName}
                          className="w-7 h-7 rounded-full object-cover border border-slate-200"
                        />
                        <span className="text-xs font-semibold text-slate-800">
                          {featuredRecipe.author?.displayName}
                        </span>
                      </div>

                      <div className="flex items-center gap-2">
                        <button
                          onClick={() => setJsonLdRecipe(featuredRecipe)}
                          className="text-xs text-blue-600 hover:text-blue-700 font-semibold px-2 py-1 rounded bg-blue-50 hover:bg-blue-100 transition cursor-pointer"
                          title="Xem JSON-LD Schema.org (SEO)"
                        >
                          SEO Data
                        </button>
                        <button
                          onClick={() => navigate(`/recipes/${featuredRecipe.slug}`)}
                          className="flex items-center gap-1 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 px-3.5 py-1.5 rounded-lg shadow-sm transition cursor-pointer"
                        >
                          <span>Xem chi tiết</span>
                          <ArrowRight className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </section>

      {/* Category Showcase Section */}
      <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col sm:flex-row sm:items-end justify-between mb-8 gap-3">
          <div>
            <div className="flex items-center gap-2 text-blue-600 text-xs font-bold uppercase tracking-wider">
              <Layers className="w-4 h-4" />
              <span>Khám phá theo danh mục</span>
            </div>
            <h2 className="text-2xl sm:text-3xl font-bold text-slate-900 mt-1">
              Phân loại Ẩm thực Phong phú
            </h2>
          </div>
          <button
            onClick={() => navigate('/categories')}
            className="text-sm font-semibold text-blue-600 hover:text-blue-700 flex items-center gap-1 cursor-pointer"
          >
            <span>Xem tất cả danh mục</span>
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-4">
          {categories.map((category) => (
            <div
              key={category.id}
              onClick={() => navigate(`/categories/${category.slug}`)}
              className="group relative rounded-2xl overflow-hidden bg-white border border-blue-100 shadow-xs hover:shadow-md hover:border-blue-300 transition-all duration-200 cursor-pointer text-center p-3 flex flex-col items-center"
            >
              <div className="w-16 h-16 sm:w-20 sm:h-20 rounded-2xl overflow-hidden mb-3 shadow-xs">
                <img
                  src={category.imageUrl}
                  alt={category.name}
                  className="w-full h-full object-cover group-hover:scale-110 transition duration-300"
                />
              </div>
              <h3 className="text-xs sm:text-sm font-bold text-slate-800 group-hover:text-blue-600 transition-colors line-clamp-1">
                {category.name}
              </h3>
              <p className="text-[11px] text-blue-700 mt-0.5 font-medium">
                {category.recipeCount} công thức
              </p>
            </div>
          ))}
        </div>
      </section>

      {/* Trending & Latest Recipes Section */}
      <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col sm:flex-row sm:items-end justify-between mb-8 gap-3">
          <div>
            <div className="flex items-center gap-2 text-blue-600 text-xs font-bold uppercase tracking-wider">
              <TrendingUp className="w-4 h-4" />
              <span>Được yêu thích nhất</span>
            </div>
            <h2 className="text-2xl sm:text-3xl font-bold text-slate-900 mt-1">
              Công Thức Đang Thịnh Hành
            </h2>
          </div>
          <button
            onClick={() => navigate('/recipes')}
            className="text-sm font-semibold text-blue-600 hover:text-blue-700 flex items-center gap-1 cursor-pointer"
          >
            <span>Xem tất cả công thức</span>
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {trendingRecipes.map((recipe) => (
            <div
              key={recipe.id}
              onClick={() => navigate(`/recipes/${recipe.slug}`)}
              className="group bg-white rounded-2xl overflow-hidden border border-slate-200/80 shadow-xs hover:shadow-xl hover:border-blue-200 transition-all duration-300 flex flex-col cursor-pointer"
            >
              {/* Image & Badges */}
              <div className="relative h-48 w-full overflow-hidden bg-slate-100">
                <img
                  src={recipe.images[0]?.mediumUrl || recipe.images[0]?.originalUrl}
                  alt={recipe.title}
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                />
                <div className="absolute top-3 left-3">
                  <span className="px-2 py-0.5 rounded-md text-[11px] font-bold bg-blue-600/90 text-white backdrop-blur-xs">
                    {recipe.difficulty === 'Hard' ? 'Khó' : recipe.difficulty === 'Medium' ? 'Trung bình' : 'Dễ'}
                  </span>
                </div>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    toggleBookmark(recipe.id);
                  }}
                  className="absolute top-3 right-3 p-2 rounded-full bg-white/90 hover:bg-white text-slate-700 shadow-sm transition cursor-pointer"
                  title="Lưu công thức"
                >
                  <Bookmark
                    className={`w-3.5 h-3.5 ${
                      bookmarks.includes(recipe.id) ? 'fill-blue-600 text-blue-600' : ''
                    }`}
                  />
                </button>
              </div>

              {/* Body */}
              <div className="p-4 flex-1 flex flex-col justify-between space-y-3">
                <div>
                  <div className="flex items-center gap-2 text-[11px] text-slate-500 mb-1.5">
                    <span className="flex items-center gap-1">
                      <Clock className="w-3 h-3 text-blue-500" />
                      {recipe.cookTimeMinutes} phút
                    </span>
                    <span>•</span>
                    <span>{recipe.servings} phần ăn</span>
                  </div>
                  <h4 className="text-sm font-bold text-slate-900 group-hover:text-blue-600 transition-colors line-clamp-2 leading-snug">
                    {recipe.title}
                  </h4>
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
                    <span className="text-[11px] text-slate-600 font-medium truncate max-w-[100px]">
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
      </section>

      {/* Value Proposition / Architectural Standards Banner */}
      <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="rounded-3xl bg-gradient-to-r from-blue-900 to-indigo-900 text-white p-8 sm:p-12 shadow-xl relative overflow-hidden">
          <div className="absolute -right-10 -bottom-10 w-72 h-72 bg-blue-500/20 rounded-full blur-2xl"></div>
          
          <div className="relative z-10 max-w-3xl space-y-4">
            <span className="inline-block px-3 py-1 rounded-full text-xs font-semibold bg-blue-700/60 text-blue-200 border border-blue-600/40">
              Kiến trúc Tiêu chuẩn Doanh nghiệp
            </span>
            <h3 className="text-2xl sm:text-3xl font-bold leading-snug">
              Nền tảng Nấu ăn Xây dựng theo Chuẩn IEEE 830 / ISO 29148
            </h3>
            <p className="text-sm text-blue-100/90 leading-relaxed">
              Tích hợp hệ thống Clean Architecture 4 tầng, CQRS Pipeline với MediatR, tìm kiếm toàn văn bản tiếng Việt tối ưu bằng PostgreSQL tsvector/unaccent, bộ nhớ đệm phân tán Redis 7 và lưu trữ ảnh MinIO S3-compatible.
            </p>

            <div className="pt-4 flex flex-wrap gap-4">
              <button
                onClick={() => navigate('/dashboard/recipes/new')}
                className="px-5 py-2.5 rounded-xl bg-blue-500 hover:bg-blue-400 text-white font-bold text-xs sm:text-sm shadow-md transition flex items-center gap-2 cursor-pointer"
              >
                <PlusCircle className="w-4 h-4" />
                <span>Đăng tải công thức mới</span>
              </button>
              <button
                onClick={() => navigate('/recipes')}
                className="px-5 py-2.5 rounded-xl bg-white/10 hover:bg-white/20 text-white font-semibold text-xs sm:text-sm border border-white/20 transition cursor-pointer"
              >
                Xem tất cả danh mục & công thức
              </button>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
