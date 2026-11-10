import { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { RecipeJsonLd } from '@/components/seo/RecipeJsonLd';
import { formatMinutes } from '@/lib/recipe-format';
import { getRecipeBySlug } from '@/lib/recipes';
import { buildRecipeMetadata } from '@/lib/seo/recipeMeta';
import type { RecipeDetailDto } from '@/lib/types';

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

/** Theo enum `RecipeDifficulty` của backend (SRS 7.2): Easy = 1 … Expert = 4 — KHÔNG bắt đầu từ 0. */
const DIFFICULTY_LABELS: Record<number, string> = {
  1: 'Dễ',
  2: 'Trung bình',
  3: 'Khó',
  4: 'Rất khó',
};

/** D97 — mốc thời gian của từng bước: "phút 0", "phút 30", "1 giờ 5 phút"… */
function formatMark(minute: number): string {
  return minute < 60 ? `phút ${minute}` : formatMinutes(minute);
}

/**
 * D97 — dòng thời gian nấu: cộng dồn `timerMinutes` để biết mỗi bước bắt đầu ở phút thứ mấy. Chỉ khi MỌI bước có
 * thời lượng; thiếu một bước thì cộng dồn sẽ sai → trả null, trang đánh dấu "Bước n".
 */
function buildTimeline(steps: RecipeDetailDto['steps']): { starts: string[]; end: string } | null {
  if (steps.length === 0 || steps.some((st) => !st.timerMinutes)) {
    return null;
  }
  let at = 0;
  const starts = steps.map((st) => {
    const mark = formatMark(at);
    at += st.timerMinutes ?? 0;
    return mark;
  });
  return { starts, end: formatMark(at) };
}

function NutritionSection({ nutrition }: { nutrition: NonNullable<RecipeDetailDto['nutrition']> }) {
  const items = [
    { label: 'Calories', value: nutrition.calories, unit: ' kcal' },
    { label: 'Protein', value: nutrition.protein, unit: 'g' },
    { label: 'Carbs', value: nutrition.carbohydrates, unit: 'g' },
    { label: 'Chất béo', value: nutrition.fat, unit: 'g' },
    { label: 'Chất xơ', value: nutrition.fiber, unit: 'g' },
    { label: 'Natri', value: nutrition.sodium, unit: 'mg' },
  ].filter((item) => item.value);

  // Có object nutrition nhưng mọi chỉ số rỗng → không vẽ khung trống.
  if (items.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="recipe-nutrition" className="rounded-2xl border border-border bg-surface p-5">
      <h2 id="recipe-nutrition" className="mb-4 text-xl font-extrabold">
        Giá trị dinh dưỡng (mỗi khẩu phần)
      </h2>
      <dl className="grid grid-cols-3 gap-3 text-center sm:grid-cols-6">
        {items.map((item) => (
          <div key={item.label} className="rounded-lg bg-surface-subtle p-2">
            <dt className="text-xs text-ink-muted">{item.label}</dt>
            <dd className="font-semibold">
              {item.value}
              {item.unit}
            </dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

/**
 * FR-RCP-002 — chi tiết công thức. D97: bố cục "sổ tay" — tiêu đề lớn + dòng thông số, ảnh rộng, "Các bước thực
 * hiện" dạng dòng thời gian theo phút, "Nguyên liệu" là danh sách đi chợ có ô tick (cột phải ≥ lg, trên các bước ở
 * màn nhỏ). Giữ neo `#buoc-n` (NFR-SEO-001) và heading "Nguyên liệu" / "Các bước thực hiện" (E2E).
 */
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

  const steps = [...recipe.steps].sort((a, b) => a.stepNumber - b.stepNumber);
  const ingredients = [...recipe.ingredients].sort((a, b) => a.orderIndex - b.orderIndex);
  const timeline = buildTimeline(steps);

  return (
    <article className="mx-auto flex max-w-5xl flex-col gap-8 px-4 py-8 md:px-8 lg:px-12">
      {/* NFR-SEO-001: Schema.org JSON-LD Recipe */}
      <RecipeJsonLd recipe={recipe} />

      <nav aria-label="Đường dẫn" className="flex flex-wrap items-center gap-2 text-sm text-ink-muted">
        <Link href="/" className="inline-flex min-h-6 items-center hover:underline">
          Trang chủ
        </Link>
        <span aria-hidden="true">/</span>
        <Link href={`/categories/${recipe.category.slug}`} className="inline-flex min-h-6 items-center hover:underline">
          {recipe.category.name}
        </Link>
        <span aria-hidden="true">/</span>
        <span aria-current="page" className="truncate text-ink">
          {recipe.title}
        </span>
      </nav>

      <header className="flex flex-col gap-4 border-b-2 border-ink pb-6">
        <p className="text-xs font-bold uppercase tracking-[0.18em] text-accent">{recipe.category.name}</p>
        <h1 className="text-5xl font-extrabold leading-none md:text-7xl">{recipe.title}</h1>
        {recipe.description && <p className="max-w-3xl text-lg leading-relaxed text-ink-muted">{recipe.description}</p>}
        <dl className="flex flex-wrap gap-x-6 gap-y-2 text-sm">
          <div className="flex flex-row-reverse gap-1.5">
            <dt className="text-ink-muted">chuẩn bị</dt>
            <dd className="font-bold">{formatMinutes(recipe.prepTime)}</dd>
          </div>
          <div className="flex flex-row-reverse gap-1.5">
            <dt className="text-ink-muted">nấu</dt>
            <dd className="font-bold">{formatMinutes(recipe.cookTime)}</dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="sr-only">Khẩu phần</dt>
            <dd className="font-bold">{recipe.servings} người</dd>
          </div>
          {DIFFICULTY_LABELS[recipe.difficulty] && (
            <div className="flex gap-1.5">
              <dt className="sr-only">Độ khó</dt>
              <dd className="font-bold">{DIFFICULTY_LABELS[recipe.difficulty]}</dd>
            </div>
          )}
        </dl>
        <p className="text-sm text-ink-muted">
          Bởi <span className="font-semibold text-ink">{recipe.author.displayName}</span>
          {' · '}
          <time dateTime={recipe.publishedAt || recipe.createdAt}>
            {new Date(recipe.publishedAt || recipe.createdAt).toLocaleDateString('vi-VN')}
          </time>
        </p>
      </header>

      <div className="relative h-[280px] w-full overflow-hidden rounded-3xl bg-brand-50 md:h-[380px]">
        {primaryImage ? (
          <Image
            src={primaryImage}
            alt={recipe.title}
            fill
            priority
            sizes="(min-width: 1200px) 900px, 100vw"
            className="object-cover"
          />
        ) : (
          <div className="flex h-full items-center justify-center text-sm text-ink-muted">Chưa có hình ảnh</div>
        )}
      </div>

      {recipe.nutrition && <NutritionSection nutrition={recipe.nutrition} />}

      {/* flex-wrap-reverse: màn nhỏ Nguyên liệu (khai báo sau) nằm trên Các bước; ≥ lg là cột phải. Trục phụ bị đảo
          nên items-end mới là căn ĐỈNH (để cột Nguyên liệu sticky từ trên). */}
      <div className="flex flex-wrap-reverse items-end gap-8">
        <section aria-labelledby="recipe-steps" className="min-w-0 flex-[999_1_480px]">
          <h2 id="recipe-steps" className="mb-6 text-3xl font-extrabold leading-none">
            Các bước thực hiện
          </h2>
          <ol>
            {steps.map((st, index) => (
              <li
                key={st.id}
                id={`buoc-${st.stepNumber}`} // NFR-SEO-001: đích của HowToStep.url
                className="flex scroll-mt-20 gap-4 md:gap-5"
              >
                <div className="w-20 flex-none pt-0.5 text-right font-display text-lg font-extrabold leading-tight text-accent md:w-24">
                  {timeline ? timeline.starts[index] : `Bước ${st.stepNumber}`}
                </div>
                <div aria-hidden="true" className="flex w-4 flex-none flex-col items-center">
                  <span className="mt-1.5 size-3.5 rounded-full bg-brand-600" />
                  <span className="w-0.5 flex-1 bg-border" />
                </div>
                <div className="flex min-w-0 flex-1 flex-col gap-1.5 pb-8">
                  <h3 className="text-lg font-bold">
                    Bước {st.stepNumber} · {st.title}
                    {st.timerMinutes ? (
                      <span className="ml-2 text-sm font-normal text-ink-muted">({formatMinutes(st.timerMinutes)})</span>
                    ) : null}
                  </h3>
                  <p className="whitespace-pre-line leading-relaxed text-ink-muted">{st.description}</p>
                  {st.imageUrl && (
                    <div className="relative mt-2 h-48 w-full overflow-hidden rounded-xl">
                      <Image src={st.imageUrl} alt={`Bước ${st.stepNumber}: ${st.title}`} fill className="object-cover" />
                    </div>
                  )}
                </div>
              </li>
            ))}
            {timeline && (
              <li className="flex gap-4 md:gap-5">
                <div className="w-20 flex-none text-right font-display text-lg font-extrabold leading-tight text-accent md:w-24">
                  {timeline.end}
                </div>
                <div aria-hidden="true" className="flex w-4 flex-none justify-center">
                  <span className="mt-1.5 size-3.5 rounded bg-accent" />
                </div>
                <p className="font-bold">Dọn ra đĩa</p>
              </li>
            )}
          </ol>
        </section>

        <section
          aria-labelledby="recipe-ingredients"
          className="flex-[1_1_280px] rounded-2xl border border-border bg-surface p-5 lg:sticky lg:top-6"
        >
          <h2 id="recipe-ingredients" className="text-2xl font-extrabold leading-none">
            Nguyên liệu
          </h2>
          <p className="mb-3 mt-1 text-sm text-ink-muted">Đi chợ cho {recipe.servings} người — tick khi đã có</p>
          <ul>
            {ingredients.map((ing) => (
              <li key={ing.id} className="border-b border-dashed border-border last:border-b-0">
                <label className="flex min-h-11 cursor-pointer items-center gap-3 py-2">
                  <input type="checkbox" className="size-[18px] flex-none accent-brand-600" />
                  <span className="flex-1">
                    {ing.name}
                    {ing.notes && <span className="block text-xs text-ink-muted">({ing.notes})</span>}
                  </span>
                  {(ing.quantity || ing.unit) && (
                    <span className="whitespace-nowrap text-sm font-semibold text-accent">
                      {[ing.quantity, ing.unit].filter(Boolean).join(' ')}
                    </span>
                  )}
                </label>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </article>
  );
}
