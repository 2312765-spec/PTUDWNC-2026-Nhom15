//Khung xương cho Breadcrumb, Tiêu đề, Thông số thời gian/khẩu phần, Ảnh lớn, Cột nguyên liệu và Các bước chế biến.

export default function RecipeDetailLoading() {
  return (
    <div
      className="container mx-auto px-4 py-8 max-w-5xl space-y-8 animate-pulse"
      role="status"
      aria-label="Đang tải chi tiết công thức..."
    >
      {/* Breadcrumb */}
      <div className="flex items-center gap-2">
        <div className="h-4 w-16 bg-slate-200 dark:bg-slate-700 rounded" />
        <span className="text-slate-300">/</span>
        <div className="h-4 w-24 bg-slate-200 dark:bg-slate-700 rounded" />
        <span className="text-slate-300">/</span>
        <div className="h-4 w-40 bg-slate-200 dark:bg-slate-700 rounded" />
      </div>

      {/* Header Info */}
      <div className="space-y-4">
        <div className="h-6 w-32 bg-slate-200 dark:bg-slate-700 rounded-full" />
        <div className="h-10 sm:h-12 w-4/5 bg-slate-200 dark:bg-slate-700 rounded-lg" />
        <div className="h-5 w-3/5 bg-slate-200 dark:bg-slate-700 rounded" />

        {/* Author & Meta */}
        <div className="flex flex-wrap items-center gap-6 pt-2">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-full bg-slate-200 dark:bg-slate-700" />
            <div className="space-y-1">
              <div className="h-4 w-28 bg-slate-200 dark:bg-slate-700 rounded" />
              <div className="h-3 w-20 bg-slate-200 dark:bg-slate-700 rounded" />
            </div>
          </div>
          <div className="h-4 w-28 bg-slate-200 dark:bg-slate-700 rounded" />
        </div>
      </div>

      {/* Hero Image Skeleton (Aspect Ratio 16:9 / 21:9) */}
      <div className="aspect-[16/9] w-full rounded-3xl bg-slate-200 dark:bg-slate-800" />

      {/* Key Specs Bar (Prep time, Cook time, Servings, Difficulty) */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 p-5 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-800">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="flex flex-col items-center gap-2">
            <div className="h-6 w-6 rounded-full bg-slate-200 dark:bg-slate-700" />
            <div className="h-3 w-16 bg-slate-200 dark:bg-slate-700 rounded" />
            <div className="h-4 w-20 bg-slate-200 dark:bg-slate-700 rounded" />
          </div>
        ))}
      </div>

      {/* Content 2-Column: Ingredients & Instructions */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 pt-4">
        {/* Left Column: Ingredients Card */}
        <div className="lg:col-span-1 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 space-y-4 h-fit">
          <div className="h-6 w-36 bg-slate-200 dark:bg-slate-700 rounded mb-4" />
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="flex justify-between items-center py-2 border-b border-slate-100 dark:border-slate-800">
              <div className="h-4 w-32 bg-slate-200 dark:bg-slate-700 rounded" />
              <div className="h-4 w-12 bg-slate-200 dark:bg-slate-700 rounded" />
            </div>
          ))}
        </div>

        {/* Right Column: Cooking Steps */}
        <div className="lg:col-span-2 space-y-6">
          <div className="h-6 w-44 bg-slate-200 dark:bg-slate-700 rounded" />
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="p-6 rounded-2xl border border-slate-200 dark:border-slate-800 space-y-3 bg-white dark:bg-slate-900">
              <div className="flex items-center gap-3">
                <div className="h-7 w-7 rounded-full bg-slate-200 dark:bg-slate-700" />
                <div className="h-5 w-32 bg-slate-200 dark:bg-slate-700 rounded" />
              </div>
              <div className="h-4 w-full bg-slate-200 dark:bg-slate-700 rounded" />
              <div className="h-4 w-5/6 bg-slate-200 dark:bg-slate-700 rounded" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}