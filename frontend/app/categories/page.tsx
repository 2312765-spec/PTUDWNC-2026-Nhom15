import type { Metadata } from 'next';
import { unstable_noStore as noStore } from 'next/cache';
import Link from 'next/link';
import { DeleteCategoryButton } from '@/components/categories/DeleteCategoryButton';
import { Badge, Card, CardBody, CardFooter } from '@/components/ui';
import { getCategories } from '@/lib/categories';

/**
 * FR-CAT-001 — SRS mục 5.1: ISR revalidate = 3600. Server Component: không cần state/effect.
 * Khi API lỗi lúc revalidate, Next giữ lại bản cũ; lần đầu lỗi thì rơi vào error.tsx.
 */
export const revalidate = 3600;

export const metadata: Metadata = {
  title: 'Danh mục công thức',
  description: 'Khám phá các danh mục món ăn và số lượng công thức đã xuất bản trong từng danh mục.',
};

export default async function CategoriesPage() {
  // `noStore()` khi API lỗi: lúc `next build` (CI/Docker không có API) nó chuyển route sang render
  // động thay vì làm vỡ build; lúc chạy thật nó ngăn cache một trang lỗi trong 3600 giây.
  const categories = await getCategories().catch((error: unknown) => {
    noStore();
    throw error;
  });

  return (
    <div className="mx-auto max-w-6xl space-y-6 px-4 py-8">
      <header className="border-b border-border pb-5">
        <h1 className="text-3xl font-bold">Danh mục công thức</h1>
        <p className="mt-1 text-sm text-ink-muted">
          Tổng hợp danh mục món ăn kèm số lượng công thức đã xuất bản.
        </p>
      </header>

      {categories.length === 0 ? (
        <Card>
          <CardBody className="py-10 text-center text-ink-muted">Chưa có danh mục nào.</CardBody>
        </Card>
      ) : (
        <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {categories.map((category) => (
            <li key={category.id}>
              <Card className="flex h-full flex-col transition-colors hover:border-brand-500">
                <Link
                  href={`/categories/${category.slug}`}
                  className="block flex-1 rounded-t-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600"
                >
                  <CardBody className="flex h-full flex-col gap-2">
                    <Badge variant="warning" className="self-start">
                      {category.recipeCount} công thức
                    </Badge>
                    <h2 className="text-lg font-semibold">{category.name}</h2>
                    <p className="line-clamp-2 text-sm text-ink-muted">
                      {category.description || 'Chưa có mô tả.'}
                    </p>
                  </CardBody>
                </Link>
                {/* FR-CAT-005 — nằm ngoài <Link> để không lồng phần tử tương tác (nút trong thẻ a). */}
                <CardFooter className="flex justify-end p-2">
                  <DeleteCategoryButton
                    categoryId={category.id}
                    categoryName={category.name}
                    recipeCount={category.recipeCount}
                  />
                </CardFooter>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
