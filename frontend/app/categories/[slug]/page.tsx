import { getCategoryBySlug } from "@/lib/categories";
import Link from "next/link";
import { notFound } from "next/navigation";

export default async function CategoryDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ page?: string }>;
}) {
  const { slug } = await params;
  const sParams = await searchParams;
  const page = parseInt(sParams.page || "1", 10);

  let data;
  try {
    data = await getCategoryBySlug(slug, page, 8);
  } catch {
    notFound();
  }

  return (
    <div className="max-w-5xl mx-auto px-4 py-8 space-y-6">
      {/* Header Danh mục */}
      <div className="border-b pb-4">
        <Link
          href="/categories"
          className="text-xs text-amber-600 hover:underline mb-2 inline-block font-semibold"
        >
          &larr; Quay lại danh sách danh mục
        </Link>
        <div className="flex justify-between items-center mt-1">
          <h1 className="text-3xl font-bold text-gray-900">{data.name}</h1>
          <span className="text-xs font-semibold px-3 py-1 bg-amber-100 text-amber-800 rounded-full">
            {data.recipeCount} công thức
          </span>
        </div>
        <p className="text-gray-600 text-sm mt-2">
          {data.description || "Danh mục chưa có mô tả."}
        </p>
      </div>

      {/* Danh sách Recipes của danh mục (Phân trang) */}
      <div className="space-y-4">
        <h2 className="text-lg font-bold text-gray-800">Công thức đã xuất bản (Published)</h2>

        {data.recipes.items.length === 0 ? (
          <div className="p-8 text-center text-gray-500 bg-gray-50 rounded-2xl border">
            Chưa có công thức nào được xuất bản trong danh mục này.
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {data.recipes.items.map((recipe) => (
              <div
                key={recipe.id}
                className="p-4 bg-white border border-gray-200 rounded-2xl hover:shadow-md transition-all flex flex-col justify-between"
              >
                <div>
                  <div className="flex justify-between text-xs text-gray-400 mb-2">
                    <span className="font-semibold text-amber-600">{recipe.difficulty}</span>
                    <span>{recipe.cookingTimeMinutes} phút</span>
                  </div>
                  <h3 className="font-bold text-gray-900 text-base">{recipe.title}</h3>
                  <p className="text-gray-600 text-xs mt-1 line-clamp-2">{recipe.summary}</p>
                </div>
                <div className="mt-3 pt-2 border-t text-[11px] text-gray-400 flex justify-between">
                  <span>Tác giả: {recipe.authorName}</span>
                  <span className="text-emerald-600 font-semibold">Published</span>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Nút phân trang (Pagination) */}
        {data.recipes.totalPages > 1 && (
          <div className="flex justify-center items-center space-x-2 pt-4">
            {data.recipes.hasPreviousPage && (
              <Link
                href={`/categories/${slug}?page=${page - 1}`}
                className="px-3 py-1.5 border rounded-lg text-xs font-semibold hover:bg-gray-50"
              >
                Trang trước
              </Link>
            )}
            <span className="px-3 py-1.5 text-xs text-gray-500">
              Trang {page} / {data.recipes.totalPages}
            </span>
            {data.recipes.hasNextPage && (
              <Link
                href={`/categories/${slug}?page=${page + 1}`}
                className="px-3 py-1.5 border rounded-lg text-xs font-semibold hover:bg-gray-50"
              >
                Trang sau
              </Link>
            )}
          </div>
        )}
      </div>
    </div>
  );
}