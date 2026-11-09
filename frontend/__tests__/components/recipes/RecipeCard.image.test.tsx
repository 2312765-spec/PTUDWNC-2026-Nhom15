import { render, screen } from '@testing-library/react';
import RecipeCard, { CARD_IMAGE_QUALITY, GRID_2_COLS_SIZES, GRID_3_COLS_SIZES } from '@/components/recipes/RecipeCard';
import nextConfig from '@/next.config';
import type { RecipeSummaryDto } from '@/lib/types';

const recipe: RecipeSummaryDto = {
  id: 'r1',
  title: 'Bánh khọt',
  slug: 'banh-khot',
  description: 'Bánh khọt giòn',
  featuredImageUrl: 'http://localhost:9000/culinary-blog/recipes/r1/a.webp',
  status: 'Published',
  prepTimeMinutes: 20,
  cookTimeMinutes: 15,
  difficulty: 'Easy',
  authorId: 'u1',
  authorName: 'Tác giả',
  createdAt: '2026-11-01T00:00:00Z',
};

const image = () => screen.getByRole('img', { name: 'Bánh khọt' });

describe('RecipeCard — ảnh (NFR-PERF-005)', () => {
  it('NFR-PERF-005/D86: card đầu tiên (LCP) không lazy (được preload — kiểm prop ở RecipeCard.quality.test)', () => {
    render(<RecipeCard recipe={recipe} isLcp />);

    expect(image()).not.toHaveAttribute('loading', 'lazy');
  });

  it('NFR-PERF-005: các card khác lazy, không tranh băng thông với ảnh LCP', () => {
    render(<RecipeCard recipe={recipe} />);

    expect(image()).toHaveAttribute('loading', 'lazy');
    expect(image()).not.toHaveAttribute('fetchpriority');
  });

  it('NFR-PERF-005: 50 có trong images.qualities — Next 16 ép quality ngoài danh sách về mức gần nhất', () => {
    expect(nextConfig.images?.qualities).toEqual(expect.arrayContaining([CARD_IMAGE_QUALITY, 75]));
  });

  it('NFR-USE-001/D84: sizes theo lưới 3 cột mặc định, truyền được lưới 2 cột', () => {
    const { unmount } = render(<RecipeCard recipe={recipe} />);
    expect(image()).toHaveAttribute('sizes', GRID_3_COLS_SIZES);
    unmount();

    render(<RecipeCard recipe={recipe} sizes={GRID_2_COLS_SIZES} />);
    expect(image()).toHaveAttribute('sizes', GRID_2_COLS_SIZES);
  });
});
