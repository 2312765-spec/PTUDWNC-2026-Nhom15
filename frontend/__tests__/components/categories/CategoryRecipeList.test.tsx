import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import CategoryRecipeList from '@/components/categories/CategoryRecipeList';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';

const recipe = (i: number, overrides: Partial<RecipeSummaryDto> = {}): RecipeSummaryDto => ({
  id: `r${i}`,
  title: `Món ${i}`,
  slug: `mon-${i}`,
  description: `Mô tả món ${i}`,
  featuredImageUrl: null,
  status: 'Published',
  prepTimeMinutes: 30,
  cookTimeMinutes: 45,
  difficulty: 'Medium',
  authorId: 'u1',
  authorName: 'Bếp Nhà',
  createdAt: '2026-10-01T00:00:00Z',
  ...overrides,
});

const paged = (items: RecipeSummaryDto[]): PagedResult<RecipeSummaryDto> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 12,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
});

function renderList(items: RecipeSummaryDto[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <CategoryRecipeList slug="mon-khai-vi" pageSize={12} initialRecipes={paged(items)} />
    </QueryClientProvider>,
  );
}

describe('CategoryRecipeList (FR-CAT-002)', () => {
  it('FR-CAT-002: mỗi công thức là link tới trang chi tiết /recipes/{slug} (như thẻ ở /recipes)', () => {
    renderList([recipe(1), recipe(2)]);

    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByRole('link', { name: /Món 1/ })).toHaveAttribute('href', '/recipes/mon-1');
    expect(within(items[1]).getByRole('link', { name: /Món 2/ })).toHaveAttribute('href', '/recipes/mon-2');
  });

  it('hiện độ khó, tổng thời gian theo giờ khi ≥ 60 phút (D97), tác giả', () => {
    renderList([recipe(1)]);

    const item = screen.getByRole('listitem');
    expect(item).toHaveTextContent('Trung bình');
    expect(item).toHaveTextContent('1 giờ 15 phút');
    expect(item).toHaveTextContent('Tác giả: Bếp Nhà');
  });

  it('danh mục chưa có công thức → thông báo trống, không có link', () => {
    renderList([]);

    expect(screen.getByText('Chưa có công thức nào được xuất bản trong danh mục này.')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
