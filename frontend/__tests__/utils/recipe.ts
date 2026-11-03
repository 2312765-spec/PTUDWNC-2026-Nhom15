import type { RecipeDetailDto } from '@/lib/types';

/** Công thức Published đầy đủ dữ liệu — mỗi test ghi đè đúng trường nó cần qua `overrides`. */
export function recipeDetail(overrides: Partial<RecipeDetailDto> = {}): RecipeDetailDto {
  return {
    id: '7b1d2c3e-0000-4000-8000-000000000001',
    title: 'Phở bò Hà Nội',
    slug: 'pho-bo-ha-noi',
    description:
      'Nước dùng ninh xương bò tám tiếng, thơm quế hồi, bánh phở mềm và thịt bò tái chín vừa tới. ' +
      'Món sáng quen thuộc của người Hà Nội, nấu được tại nhà với nguyên liệu dễ tìm.',
    difficulty: 1,
    status: 1,
    prepTime: 30,
    cookTime: 480,
    servings: 4,
    publishedAt: '2026-10-01T02:00:00Z',
    createdAt: '2026-09-30T08:00:00Z',
    updatedAt: '2026-10-05T09:30:00Z',
    category: { id: 'c1', name: 'Món nước', slug: 'mon-nuoc' },
    author: { id: 'u1', displayName: 'Nguyễn Văn An' },
    nutrition: { calories: 450, protein: 30, carbohydrates: 55, fat: 12, fiber: 3, sodium: 1200 },
    ingredients: [
      { id: 'i2', name: 'bánh phở', quantity: 500, unit: 'g', orderIndex: 1 },
      { id: 'i1', name: 'xương bò', quantity: 1.5, unit: 'kg', notes: 'chần sơ', orderIndex: 0 },
      { id: 'i3', name: 'hành lá', orderIndex: 2 },
    ],
    steps: [
      { id: 's2', stepNumber: 2, title: 'Ninh nước dùng', description: 'Ninh xương 8 tiếng với quế, hồi.' },
      {
        id: 's1',
        stepNumber: 1,
        title: 'Sơ chế xương',
        description: 'Chần xương bò qua nước sôi rồi rửa sạch.',
        imageUrl: 'https://culinary.example/culinary-blog/recipes/x/step1.jpg',
      },
    ],
    images: [
      {
        id: 'img2',
        originalUrl: 'https://culinary.example/culinary-blog/recipes/x/b.jpg',
        isPrimary: false,
        orderIndex: 1,
      },
      {
        id: 'img1',
        originalUrl: 'https://culinary.example/culinary-blog/recipes/x/a.jpg',
        altText: 'Tô phở bò',
        isPrimary: true,
        orderIndex: 0,
      },
    ],
    ...overrides,
  };
}
