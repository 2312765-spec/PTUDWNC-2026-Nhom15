export default function CategoriesLoading() {
  return (
    <div
      className="container mx-auto px-4 py-8 space-y-8 animate-pulse"
      role="status"
      aria-label="Đang tải danh mục..."
    >
      {/* Title & Description */}
      <div className="text-center max-w-xl mx-auto space-y-3">
        <div className="h-9 sm:h-10 w-64 bg-slate-200 dark:bg-slate-700 rounded-lg mx-auto" />
        <div className="h-4 sm:h-5 w-80 bg-slate-200 dark:bg-slate-700 rounded mx-auto" />
      </div>

      {/* Category Grid Skeleton */}
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {Array.from({ length: 8 }).map((_, i) => (
          <div
            key={i}
            className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 space-y-4 shadow-sm"
          >
            {/* Category Icon / Thumbnail */}
            <div className="h-14 w-14 rounded-2xl bg-slate-200 dark:bg-slate-800" />
            <div className="space-y-2">
              <div className="h-5 w-36 bg-slate-200 dark:bg-slate-700 rounded" />
              <div className="h-4 w-full bg-slate-200 dark:bg-slate-800 rounded" />
              <div className="h-4 w-2/3 bg-slate-200 dark:bg-slate-800 rounded" />
            </div>
            <div className="pt-2 flex justify-between items-center">
              <div className="h-5 w-20 bg-slate-200 dark:bg-slate-800 rounded-full" />
              <div className="h-4 w-12 bg-slate-200 dark:bg-slate-800 rounded" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}