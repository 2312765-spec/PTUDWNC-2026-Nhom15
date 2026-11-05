import { render, screen } from '@testing-library/react';
import RecipeCard from '@/components/recipes/RecipeCard';
import type { RecipeSummaryDto } from '@/lib/types';

const payload = '<img src=x onerror="alert(1)"><script>alert(2)</script>';

const recipe: RecipeSummaryDto = {
  id: 'r1',
  title: payload,
  slug: 'mon-doc-hai',
  description: payload,
  featuredImageUrl: null,
  status: 'Published',
  prepTimeMinutes: 5,
  cookTimeMinutes: 0,
  difficulty: 'Easy',
  authorId: 'u1',
  authorName: payload,
  createdAt: '2026-11-01T00:00:00Z',
};

describe('RecipeCard — XSS (NFR-SEC-004)', () => {
  it('NFR-SEC-004: tiêu đề/mô tả chứa HTML hiển thị nguyên văn dạng chữ, không thành thẻ', () => {
    const { container } = render(<RecipeCard recipe={recipe} />);

    expect(screen.getAllByText(payload, { exact: false }).length).toBeGreaterThan(0);
    expect(container.querySelector('img[src="x"]')).toBeNull();
    expect(container.querySelector('script')).toBeNull();
  });
});
