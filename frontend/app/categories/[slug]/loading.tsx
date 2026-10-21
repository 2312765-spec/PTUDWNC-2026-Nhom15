//Khung xương cho Breadcrumb, Tiêu đề danh mục, Thanh lọc/sắp xếp, Danh sách công thức phân trang.
export default function CategoryDetailLoading() {
  return (
    <div
      className="container mx-auto px-4 py-8 space-y-8 animate-pulse"
      role="status"
      aria-label="Đang tải danh mục và công thức..."
    >
      {/* Breadcrumb */}
      <div className="flex items-center gap-2">
        <div className="h-4 w-16 bg-slate-200 dark:bg-slate-700 rounded" />
        <span className="text-slate-300">/</span>
        <div className="h-4 w-20 bg-slate-200 dark:bg-slate-700 rounded" />
        <span className="text-slate-300">/</span>
        <div className="h-4 w-32 bg-slate-200 dark:bg-slate-700 rounded" />
      </div>

      {/* Category Banner Skeleton */}
      <div className="rounded-3xl bg-slate-100 dark:bg-slate-800/60 p-8 space-y-3">
        <div className="h-8 sm:h-10 w-64 bg-slate-200 dark:bg-slate-700 rounded-lg" />
        <div className="h-4 sm:h-5 w-3/4 max-w-xl bg-slate-200 dark:bg-slate-700 rounded" />
        <div className="h-6 w-28 bg-slate-200 dark:bg-slate-700 rounded-full mt-2" />
      </div>

      {/* Filter / Sort Bar Skeleton */}
      <div className="flex flex-wrap justify-between items-center gap-4 py-2 border-b border-slate-200 dark:border-slate-800">
        <div className="h-10 w-48 bg-slate-200 dark:bg-slate-700 rounded-lg" />
        <div className="h-10 w-36 bg-slate-200 dark:bg-slate-700 rounded-lg" />
      </div>

      {/* Recipe Grid Skeleton */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <div
            key={i}
            className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-sm"
          >
            <div className="aspect-video w-full bg-slate-200 dark:bg-slate-800" />
            <div className="p-5 space-y-3">
              <div className="h-5 w-24 bg-slate-200 dark:bg-slate-800 rounded-full" />
              <div className="h-6 w-full bg-slate-200 dark:bg-slate-800 rounded" />
              <div className="h-4 w-3/4 bg-slate-200 dark:bg-slate-800 rounded" />
              <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex justify-between items-center">
                <div className="h-4 w-24 bg-slate-200 dark:bg-slate-800 rounded" />
                <div className="h-4 w-16 bg-slate-200 dark:bg-slate-800 rounded" />
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Pagination Skeleton */}
      <div className="flex justify-center items-center gap-2 pt-6">
        <div className="h-10 w-24 bg-slate-200 dark:bg-slate-700 rounded-lg" />
        <div className="h-10 w-10 bg-slate-200 dark:bg-slate-700 rounded-lg" />
        <div className="h-10 w-10 bg-slate-200 dark:bg-slate-700 rounded-lg" />
        <div className="h-10 w-24 bg-slate-200 dark:bg-slate-700 rounded-lg" />
      </div>
    </div>
  );
}