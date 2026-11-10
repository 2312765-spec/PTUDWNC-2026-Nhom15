import { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { RecipeJsonLd } from '@/components/seo/RecipeJsonLd';
import { getRecipeBySlug } from '@/lib/recipes';
import { buildRecipeMetadata } from '@/lib/seo/recipeMeta';

export const revalidate = 300; // SRS 5.1: ISR 5 phút (300 giây)

/**
 * SRS 5.1 — ISR theo yêu cầu: không build sẵn slug nào, render lần đầu khi có request rồi cache theo
 * `revalidate`. Thiếu hàm này Next coi route động là render mỗi request (ƒ) dù đã khai báo revalidate.
 */
export function generateStaticParams() {
  return [];
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const recipe = await getRecipeBySlug(slug);

  // Đuôi "| Culinary Blog" do template của app/layout.tsx thêm — không tự ghép (NFR-SEO-002)
  if (!recipe) {
    return { title: 'Không tìm thấy công thức' };
  }

  // NFR-SEO-002: title ≤ 60, description 150–160, canonical, OG, Twitter, noindex Draft/Archived (D61)
  return buildRecipeMetadata(recipe);
}

export default async function RecipeDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const recipe = await getRecipeBySlug(slug);

  // Không thêm loading.tsx cho segment này hoặc segment cha: loading.tsx bật streaming nên header 200
  // được gửi trước notFound() → soft 404 (e2e/recipe.spec.ts kiểm tra status 404).
  if (!recipe) {
    notFound();
  }

  // Chưa có ảnh → khung "Chưa có hình ảnh" như RecipeCard (frontend không có thư mục public/ chứa ảnh placeholder).
  const primaryImage =
    recipe.images.find((img) => img.isPrimary)?.originalUrl || recipe.images[0]?.originalUrl;

  const difficultyLabels: Record<number, string> = {
    0: 'Dễ',
    1: 'Trung bình',
    2: 'Khó',
  };

  return (
    <article className="max-w-4xl mx-auto px-4 py-8">
      {/* NFR-SEO-001: Schema.org JSON-LD Recipe */}
      <RecipeJsonLd recipe={recipe} />

      {/* Breadcrumb */}
      <nav className="text-sm text-ink-muted mb-6 flex items-center space-x-2">
        <Link href="/" className="inline-flex min-h-6 items-center hover:underline">
          Trang chủ
        </Link>
        <span>/</span>
        <Link href={`/categories/${recipe.category.slug}`} className="inline-flex min-h-6 items-center hover:underline">
          {recipe.category.name}
        </Link>
        <span>/</span>
        <span className="text-ink font-medium truncate">{recipe.title}</span>
      </nav>

      {/* Header */}
      <header className="mb-8">
        <h1 className="text-3xl md:text-4xl font-bold text-ink mb-4">
          {recipe.title}
        </h1>
        <p className="text-lg text-ink-muted mb-6 leading-relaxed">
          {recipe.description}
        </p>

        {/* Tác giả & Ngày đăng */}
        <div className="flex items-center space-x-4 border-y border-border py-3 text-sm text-ink-muted">
          <div className="flex items-center space-x-2">
            <span className="font-semibold text-ink">
              {recipe.author.displayName}
            </span>
          </div>
          <span>•</span>
          <time dateTime={recipe.publishedAt || recipe.createdAt}>
            {new Date(recipe.publishedAt || recipe.createdAt).toLocaleDateString('vi-VN')}
          </time>
          <span>•</span>
          <span className="bg-brand-50 text-brand-700 px-2 py-0.5 rounded text-xs font-medium">
            {recipe.category.name}
          </span>
        </div>
      </header>

      {/* Main Image */}
      <div className="relative w-full h-[350px] md:h-[450px] rounded-xl overflow-hidden mb-8 shadow-sm bg-surface-subtle">
        {primaryImage ? (
          <Image
            src={primaryImage}
            alt={recipe.title}
            fill
            priority
            className="object-cover"
          />
        ) : (
          <div className="flex h-full items-center justify-center text-sm text-ink-muted">Chưa có hình ảnh</div>
        )}
      </div>

      {/* Meta Grid (Thời gian, khẩu phần, độ khó) */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 p-4 bg-brand-50/50 rounded-xl mb-10 border border-brand-100/60">
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-ink-muted mb-1">Chuẩn bị</span>
          <span className="text-lg font-bold text-ink">{recipe.prepTime} phút</span>
        </div>
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-ink-muted mb-1">Nấu nướng</span>
          <span className="text-lg font-bold text-ink">{recipe.cookTime} phút</span>
        </div>
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-ink-muted mb-1">Khẩu phần</span>
          <span className="text-lg font-bold text-ink">{recipe.servings} người</span>
        </div>
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-ink-muted mb-1">Độ khó</span>
          <span className="text-lg font-bold text-brand-700">
            {difficultyLabels[recipe.difficulty] || 'Dễ'}
          </span>
        </div>
      </div>

      {/* Dinh dưỡng (Nutrition) */}
      {recipe.nutrition && (
        <section className="mb-10 p-5 bg-surface rounded-xl border border-border shadow-sm">
          <h2 className="text-xl font-bold text-ink mb-4">
            Giá trị dinh dưỡng (mỗi khẩu phần)
          </h2>
          <div className="grid grid-cols-3 sm:grid-cols-6 gap-3 text-center">
            {recipe.nutrition.calories && (
              <div className="p-2 bg-surface-subtle rounded">
                <span className="block text-xs text-ink-muted">Calories</span>
                <span className="font-semibold text-ink">{recipe.nutrition.calories} kcal</span>
              </div>
            )}
            {recipe.nutrition.protein && (
              <div className="p-2 bg-surface-subtle rounded">
                <span className="block text-xs text-ink-muted">Protein</span>
                <span className="font-semibold text-ink">{recipe.nutrition.protein}g</span>
              </div>
            )}
            {recipe.nutrition.carbohydrates && (
              <div className="p-2 bg-surface-subtle rounded">
                <span className="block text-xs text-ink-muted">Carbs</span>
                <span className="font-semibold text-ink">{recipe.nutrition.carbohydrates}g</span>
              </div>
            )}
            {recipe.nutrition.fat && (
              <div className="p-2 bg-surface-subtle rounded">
                <span className="block text-xs text-ink-muted">Chất béo</span>
                <span className="font-semibold text-ink">{recipe.nutrition.fat}g</span>
              </div>
            )}
            {recipe.nutrition.fiber && (
              <div className="p-2 bg-surface-subtle rounded">
                <span className="block text-xs text-ink-muted">Chất xơ</span>
                <span className="font-semibold text-ink">{recipe.nutrition.fiber}g</span>
              </div>
            )}
            {recipe.nutrition.sodium && (
              <div className="p-2 bg-surface-subtle rounded">
                <span className="block text-xs text-ink-muted">Natri</span>
                <span className="font-semibold text-ink">{recipe.nutrition.sodium}mg</span>
              </div>
            )}
          </div>
        </section>
      )}

      {/* Layout 2 cột: Nguyên liệu & Các bước */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-10">
        {/* Cột nguyên liệu */}
        <section className="md:col-span-1">
          <div className="sticky top-6 p-6 bg-surface-subtle rounded-xl border border-border">
            <h2 className="text-xl font-bold text-ink mb-4 pb-2 border-b border-border">
              Nguyên liệu
            </h2>
            <ul className="space-y-3">
              {recipe.ingredients.map((ing) => (
                <li key={ing.id} className="text-sm text-ink-muted flex justify-between items-baseline">
                  <span>
                    <strong className="font-medium text-ink">{ing.name}</strong>
                    {ing.notes && <span className="text-xs text-ink-muted block">({ing.notes})</span>}
                  </span>
                  <span className="text-ink-muted font-medium ml-2 whitespace-nowrap">
                    {ing.quantity ? `${ing.quantity} ` : ''}
                    {ing.unit || ''}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </section>

        {/* Cột các bước thực hiện */}
        <section className="md:col-span-2">
          <h2 className="text-2xl font-bold text-ink mb-6">
            Các bước thực hiện
          </h2>
          <div className="space-y-6">
            {recipe.steps.map((st) => (
              <div
                key={st.id}
                id={`buoc-${st.stepNumber}`} // NFR-SEO-001: đích của HowToStep.url
                className="scroll-mt-20 p-5 rounded-xl border border-border bg-surface shadow-sm hover:border-brand-200 transition"
              >
                <div className="flex items-center space-x-3 mb-3">
                  <span className="flex items-center justify-center w-8 h-8 rounded-full bg-brand-600 text-on-brand font-bold text-sm">
                    {st.stepNumber}
                  </span>
                  <h3 className="font-semibold text-lg text-ink">{st.title}</h3>
                </div>

                <p className="text-ink-muted leading-relaxed mb-3 whitespace-pre-line">
                  {st.description}
                </p>

                {st.timerMinutes && (
                  <div className="inline-flex items-center space-x-1.5 text-xs text-warning bg-warning-subtle px-2.5 py-1 rounded-md mb-2 font-medium">
                    <span>⏱ Thời gian: {st.timerMinutes} phút</span>
                  </div>
                )}

                {st.imageUrl && (
                  <div className="relative w-full h-48 rounded-lg overflow-hidden mt-3">
                    <Image
                      src={st.imageUrl}
                      alt={`Bước ${st.stepNumber}: ${st.title}`}
                      fill
                      className="object-cover"
                    />
                  </div>
                )}
              </div>
            ))}
          </div>
        </section>
      </div>
    </article>
  );
}