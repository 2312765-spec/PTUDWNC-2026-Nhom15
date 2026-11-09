import { render } from '@testing-library/react';
import RecipeCard, { CARD_IMAGE_QUALITY } from '@/components/recipes/RecipeCard';
import type { RecipeSummaryDto } from '@/lib/types';

// Jest không nạp next.config.ts nên next/image thật ép mọi quality về 75 — kiểm tra props truyền vào thay vì URL.
const imageProps: Record<string, unknown>[] = [];
jest.mock('next/image', () => ({
  __esModule: true,
  default: (props: Record<string, unknown>) => {
    imageProps.push(props);
    return null;
  },
}));

const recipe = {
  id: 'r1',
  title: 'Bánh khọt',
  slug: 'banh-khot',
  description: '',
  featuredImageUrl: 'http://localhost:9000/culinary-blog/recipes/r1/a.webp',
  status: 'Published',
  prepTimeMinutes: 20,
  cookTimeMinutes: 15,
  difficulty: 'Easy',
  authorId: 'u1',
  authorName: null,
  createdAt: '2026-11-01T00:00:00Z',
} as RecipeSummaryDto;

it('NFR-PERF-005: ảnh card xin quality = CARD_IMAGE_QUALITY (50)', () => {
  render(<RecipeCard recipe={recipe} />);

  expect(CARD_IMAGE_QUALITY).toBe(50);
  expect(imageProps.at(-1)).toMatchObject({ quality: 50 });
});
