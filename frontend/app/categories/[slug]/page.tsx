import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import CategoryRecipeList from '@/components/categories/CategoryRecipeList';
import { Badge } from '@/components/ui';
import { getCategoryBySlug } from '@/lib/categories';

/** FR-CAT-002 — SRS mục 5.1: ISR revalidate = 600. */
export const revalidate = 600;

const PAGE_SIZE = 8;

/**
 * Không prerender slug nào lúc build (build không cần API chạy); mỗi slug được sinh khi có
 * request đầu tiên rồi cache theo `revalidate` ở trên.
 */
export function generateStaticParams() {
  return [];
}

interface PageProps {
  params: Promise<{ slug: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const data = await getCategoryBySlug(slug, 1, PAGE_SIZE);
  if (!data) return { title: 'Không tìm thấy danh mục' };

  return {
    title: data.category.name,
    description: data.category.description ?? `Công thức thuộc danh mục ${data.category.name}.`,
  };
}

export default async function CategoryDetailPage({ params }: PageProps) {
  const { slug } = await params;
  const data = await getCategoryBySlug(slug, 1, PAGE_SIZE);
  if (!data) notFound();

  const { category, recipes } = data;

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-8">
      <header className="border-b border-border pb-4">
        <Link href="/categories" className="text-sm font-medium text-brand-700 hover:underline">
          &larr; Tất cả danh mục
        </Link>
        <div className="mt-2 flex items-center justify-between gap-3">
          <h1 className="text-3xl font-bold">{category.name}</h1>
          <Badge variant="warning">{category.recipeCount} công thức</Badge>
        </div>
        <p className="mt-2 text-sm text-ink-muted">
          {category.description || 'Danh mục chưa có mô tả.'}
        </p>
      </header>

      <section aria-labelledby="recipes-heading" className="space-y-4">
        <h2 id="recipes-heading" className="text-lg font-semibold">
          Công thức trong danh mục
        </h2>
        <CategoryRecipeList slug={slug} pageSize={PAGE_SIZE} initialRecipes={recipes} />
      </section>
    </div>
  );
}
