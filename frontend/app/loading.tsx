//Khung xương cho Hero banner, thanh danh mục món ăn và lưới công thức nổi bật (Featured Recipes).

export default function HomeLoading() {
  return (
    <div
      className="container mx-auto px-4 py-8 space-y-12 animate-pulse"
      role="status"
      aria-label="Đang tải trang chủ..."
    >
      {/* Hero Section Skeleton */}
      <div className="relative overflow-hidden rounded-3xl bg-slate-100 dark:bg-slate-800/60 p-8 sm:p-12 text-center flex flex-col items-center">
        <div className="h-8 sm:h-12 w-3/4 max-w-xl bg-slate-200 dark:bg-slate-700 rounded-lg mb-4" />
        <div className="h-4 sm:h-5 w-1/2 max-w-md bg-slate-200 dark:bg-slate-700 rounded mb-8" />
        {/* Search Bar Skeleton */}
        <div className="w-full max-w-xl h-14 bg-slate-200 dark:bg-slate-700 rounded-full" />
      </div>

      {/* Categories Bar Skeleton */}
      <div className="space-y-4">
        <div className="h-6 w-48 bg-slate-200 dark:bg-slate-700 rounded" />
        <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-none">
          {Array.from({ length: 7 }).map((_, i) => (
            <div
              key={i}
              className="h-10 w-28 shrink-0 bg-slate-200 dark:bg-slate-700 rounded-full"
            />
          ))}
        </div>
      </div>

      {/* Featured Recipes Grid Skeleton */}
      <div className="space-y-6">
        <div className="flex justify-between items-center">
          <div className="h-7 w-56 bg-slate-200 dark:bg-slate-700 rounded" />
          <div className="h-5 w-24 bg-slate-200 dark:bg-slate-700 rounded" />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <div
              key={i}
              className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-sm"
            >
              {/* Thumbnail 16:9 */}
              <div className="aspect-video w-full bg-slate-200 dark:bg-slate-800" />
              {/* Content */}
              <div className="p-5 space-y-3">
                <div className="flex items-center gap-2">
                  <div className="h-5 w-20 bg-slate-200 dark:bg-slate-800 rounded-full" />
                  <div className="h-5 w-16 bg-slate-200 dark:bg-slate-800 rounded-full" />
                </div>
                <div className="h-6 w-full bg-slate-200 dark:bg-slate-800 rounded" />
                <div className="h-4 w-3/4 bg-slate-200 dark:bg-slate-800 rounded" />
                {/* Author footer */}
                <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center gap-3">
                  <div className="h-8 w-8 rounded-full bg-slate-200 dark:bg-slate-800" />
                  <div className="h-4 w-28 bg-slate-200 dark:bg-slate-800 rounded" />
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}