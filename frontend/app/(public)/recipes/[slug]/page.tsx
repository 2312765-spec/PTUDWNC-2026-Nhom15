import { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';

export const revalidate = 300; // SRS 5.1: ISR 5 phút (300 giây)

interface RecipeDetailDto {
  id: string;
  title: string;
  slug: string;
  description: string;
  instructions?: string;
  difficulty: number;
  status: number;
  prepTime: number;
  cookTime: number;
  servings: number;
  publishedAt?: string;
  createdAt: string;
  updatedAt?: string;
  category: {
    id: string;
    name: string;
    slug: string;
  };
  author: {
    id: string;
    displayName: string;
    avatarUrl?: string;
  };
  nutrition?: {
    calories?: number;
    protein?: number;
    carbohydrates?: number;
    fat?: number;
    fiber?: number;
    sodium?: number;
  };
  ingredients: Array<{
    id: string;
    name: string;
    quantity?: number;
    unit?: string;
    notes?: string;
    orderIndex: number;
  }>;
  steps: Array<{
    id: string;
    stepNumber: number;
    title: string;
    description: string;
    timerMinutes?: number;
    imageUrl?: string;
  }>;
  images: Array<{
    id: string;
    originalUrl: string;
    altText?: string;
    isPrimary: boolean;
    orderIndex: number;
  }>;
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

async function getRecipe(slug: string): Promise<RecipeDetailDto | null> {
  try {
    const res = await fetch(`${API_BASE_URL}/api/v1/recipes/${slug}`, {
      next: { revalidate: 300, tags: ['recipes', `recipe:${slug}`] },
    });

    if (res.status === 404 || res.status === 403) {
      return null;
    }

    if (!res.ok) {
      return null;
    }

    return await res.json();
  } catch (error) {
    return null;
  }
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const recipe = await getRecipe(slug);

  if (!recipe) {
    return {
      title: 'Không tìm thấy công thức | Culinary Blog',
    };
  }

  const primaryImage =
    recipe.images.find((img) => img.isPrimary)?.originalUrl ||
    recipe.images[0]?.originalUrl;

  const isPublished = recipe.status === 1; // 1 = Published

  return {
    title: `${recipe.title} | Culinary Blog`,
    description: recipe.description,
    openGraph: {
      title: recipe.title,
      description: recipe.description,
      type: 'article',
      publishedTime: recipe.publishedAt || recipe.createdAt,
      images: primaryImage ? [{ url: primaryImage }] : [],
    },
    // NFR-SEO: Draft hoặc Archived thì không cho Google index
    robots: isPublished
      ? { index: true, follow: true }
      : { index: false, follow: false },
  };
}

export default async function RecipeDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const recipe = await getRecipe(slug);

  if (!recipe) {
    notFound();
  }

  const primaryImage =
    recipe.images.find((img) => img.isPrimary)?.originalUrl ||
    recipe.images[0]?.originalUrl ||
    '/placeholder-recipe.jpg';

  const difficultyLabels: Record<number, string> = {
    0: 'Dễ',
    1: 'Trung bình',
    2: 'Khó',
  };

  // NFR-SEO-001: Schema.org JSON-LD kiểu Recipe
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Recipe',
    name: recipe.title,
    image: primaryImage,
    description: recipe.description,
    prepTime: `PT${recipe.prepTime}M`,
    cookTime: `PT${recipe.cookTime}M`,
    totalTime: `PT${recipe.prepTime + recipe.cookTime}M`,
    recipeYield: `${recipe.servings} phần`,
    recipeCategory: recipe.category.name,
    author: {
      '@type': 'Person',
      name: recipe.author.displayName,
    },
    nutrition: recipe.nutrition
      ? {
          '@type': 'NutritionInformation',
          calories: recipe.nutrition.calories ? `${recipe.nutrition.calories} calories` : undefined,
          proteinContent: recipe.nutrition.protein ? `${recipe.nutrition.protein}g` : undefined,
          fatContent: recipe.nutrition.fat ? `${recipe.nutrition.fat}g` : undefined,
          carbohydrateContent: recipe.nutrition.carbohydrates ? `${recipe.nutrition.carbohydrates}g` : undefined,
        }
      : undefined,
    recipeIngredient: recipe.ingredients.map(
      (ing) => `${ing.quantity ? ing.quantity + ' ' : ''}${ing.unit ? ing.unit + ' ' : ''}${ing.name}`
    ),
    recipeInstructions: recipe.steps.map((st) => ({
      '@type': 'HowToStep',
      name: st.title,
      text: st.description,
      position: st.stepNumber,
    })),
  };

  return (
    <article className="max-w-4xl mx-auto px-4 py-8">
      {/* Schema.org JSON-LD */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />

      {/* Breadcrumb */}
      <nav className="text-sm text-gray-500 mb-6 flex items-center space-x-2">
        <Link href="/" className="hover:underline">
          Trang chủ
        </Link>
        <span>/</span>
        <Link href={`/categories/${recipe.category.slug}`} className="hover:underline">
          {recipe.category.name}
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium truncate">{recipe.title}</span>
      </nav>

      {/* Header */}
      <header className="mb-8">
        <h1 className="text-3xl md:text-4xl font-bold text-gray-900 mb-4">
          {recipe.title}
        </h1>
        <p className="text-lg text-gray-600 mb-6 leading-relaxed">
          {recipe.description}
        </p>

        {/* Tác giả & Ngày đăng */}
        <div className="flex items-center space-x-4 border-y border-gray-100 py-3 text-sm text-gray-600">
          <div className="flex items-center space-x-2">
            <span className="font-semibold text-gray-900">
              {recipe.author.displayName}
            </span>
          </div>
          <span>•</span>
          <time dateTime={recipe.publishedAt || recipe.createdAt}>
            {new Date(recipe.publishedAt || recipe.createdAt).toLocaleDateString('vi-VN')}
          </time>
          <span>•</span>
          <span className="bg-orange-50 text-orange-700 px-2 py-0.5 rounded text-xs font-medium">
            {recipe.category.name}
          </span>
        </div>
      </header>

      {/* Main Image */}
      <div className="relative w-full h-[350px] md:h-[450px] rounded-xl overflow-hidden mb-8 shadow-sm">
        <Image
          src={primaryImage}
          alt={recipe.title}
          fill
          priority
          className="object-cover"
        />
      </div>

      {/* Meta Grid (Thời gian, khẩu phần, độ khó) */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 p-4 bg-orange-50/50 rounded-xl mb-10 border border-orange-100/60">
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-gray-500 mb-1">Chuẩn bị</span>
          <span className="text-lg font-bold text-gray-900">{recipe.prepTime} phút</span>
        </div>
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-gray-500 mb-1">Nấu nướng</span>
          <span className="text-lg font-bold text-gray-900">{recipe.cookTime} phút</span>
        </div>
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-gray-500 mb-1">Khẩu phần</span>
          <span className="text-lg font-bold text-gray-900">{recipe.servings} người</span>
        </div>
        <div className="text-center">
          <span className="block text-xs uppercase tracking-wider text-gray-500 mb-1">Độ khó</span>
          <span className="text-lg font-bold text-orange-600">
            {difficultyLabels[recipe.difficulty] || 'Dễ'}
          </span>
        </div>
      </div>

      {/* Dinh dưỡng (Nutrition) */}
      {recipe.nutrition && (
        <section className="mb-10 p-5 bg-white rounded-xl border border-gray-100 shadow-sm">
          <h2 className="text-xl font-bold text-gray-900 mb-4">
            Giá trị dinh dưỡng (mỗi khẩu phần)
          </h2>
          <div className="grid grid-cols-3 sm:grid-cols-6 gap-3 text-center">
            {recipe.nutrition.calories && (
              <div className="p-2 bg-gray-50 rounded">
                <span className="block text-xs text-gray-500">Calories</span>
                <span className="font-semibold text-gray-900">{recipe.nutrition.calories} kcal</span>
              </div>
            )}
            {recipe.nutrition.protein && (
              <div className="p-2 bg-gray-50 rounded">
                <span className="block text-xs text-gray-500">Protein</span>
                <span className="font-semibold text-gray-900">{recipe.nutrition.protein}g</span>
              </div>
            )}
            {recipe.nutrition.carbohydrates && (
              <div className="p-2 bg-gray-50 rounded">
                <span className="block text-xs text-gray-500">Carbs</span>
                <span className="font-semibold text-gray-900">{recipe.nutrition.carbohydrates}g</span>
              </div>
            )}
            {recipe.nutrition.fat && (
              <div className="p-2 bg-gray-50 rounded">
                <span className="block text-xs text-gray-500">Chất béo</span>
                <span className="font-semibold text-gray-900">{recipe.nutrition.fat}g</span>
              </div>
            )}
            {recipe.nutrition.fiber && (
              <div className="p-2 bg-gray-50 rounded">
                <span className="block text-xs text-gray-500">Chất xơ</span>
                <span className="font-semibold text-gray-900">{recipe.nutrition.fiber}g</span>
              </div>
            )}
            {recipe.nutrition.sodium && (
              <div className="p-2 bg-gray-50 rounded">
                <span className="block text-xs text-gray-500">Natri</span>
                <span className="font-semibold text-gray-900">{recipe.nutrition.sodium}mg</span>
              </div>
            )}
          </div>
        </section>
      )}

      {/* Layout 2 cột: Nguyên liệu & Các bước */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-10">
        {/* Cột nguyên liệu */}
        <section className="md:col-span-1">
          <div className="sticky top-6 p-6 bg-gray-50 rounded-xl border border-gray-100">
            <h2 className="text-xl font-bold text-gray-900 mb-4 pb-2 border-b border-gray-200">
              Nguyên liệu
            </h2>
            <ul className="space-y-3">
              {recipe.ingredients.map((ing) => (
                <li key={ing.id} className="text-sm text-gray-700 flex justify-between items-baseline">
                  <span>
                    <strong className="font-medium text-gray-900">{ing.name}</strong>
                    {ing.notes && <span className="text-xs text-gray-500 block">({ing.notes})</span>}
                  </span>
                  <span className="text-gray-600 font-medium ml-2 whitespace-nowrap">
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
          <h2 className="text-2xl font-bold text-gray-900 mb-6">
            Các bước thực hiện
          </h2>
          <div className="space-y-6">
            {recipe.steps.map((st) => (
              <div
                key={st.id}
                className="p-5 rounded-xl border border-gray-100 bg-white shadow-sm hover:border-orange-200 transition"
              >
                <div className="flex items-center space-x-3 mb-3">
                  <span className="flex items-center justify-center w-8 h-8 rounded-full bg-orange-600 text-white font-bold text-sm">
                    {st.stepNumber}
                  </span>
                  <h3 className="font-semibold text-lg text-gray-900">{st.title}</h3>
                </div>

                <p className="text-gray-700 leading-relaxed mb-3 whitespace-pre-line">
                  {st.description}
                </p>

                {st.timerMinutes && (
                  <div className="inline-flex items-center space-x-1.5 text-xs text-amber-700 bg-amber-50 px-2.5 py-1 rounded-md mb-2 font-medium">
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