import { screen } from '@testing-library/react';
import RecipesPage, { dynamic, metadata } from '@/app/(public)/recipes/page';
import { getCategories } from '@/lib/categories';
import type { RecipeListFilters } from '@/lib/recipes';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/categories', () => ({
  ...jest.requireActual('@/lib/categories'),
  getCategories: jest.fn(),
}));

jest.mock('@/app/(public)/recipes/RecipeResults', () => ({
  __esModule: true,
  default: ({ filters }: { filters: RecipeListFilters }) => (
    <pre data-testid="results">{JSON.stringify(filters)}</pre>
  ),
}));

const mockGetCategories = getCategories as jest.MockedFunction<typeof getCategories>;

const props = (value: Record<string, string | string[]> = {}) => ({
  searchParams: Promise.resolve(value),
});

const renderedFilters = () => JSON.parse(screen.getByTestId('results').textContent ?? '{}');

beforeEach(() => {
  mockGetCategories.mockReset();
  mockGetCategories.mockResolvedValue([
    { id: 'c1', name: 'Món chay', slug: 'mon-chay', description: null, recipeCount: 3 },
  ]);
});

describe('/recipes — FR-RCP-001', () => {
  it('FR-RCP-001: SRS 5.1 — trang render SSR (force-dynamic), có title', () => {
    expect(dynamic).toBe('force-dynamic');
    expect(metadata.title).toBe('Công thức');
  });

  it('FR-RCP-001: có h1, bộ lọc với danh mục từ API và danh sách', async () => {
    renderWithProviders(await RecipesPage(props()));

    expect(screen.getByRole('heading', { level: 1, name: 'Công thức nấu ăn' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Món chay' })).toBeInTheDocument();
    expect(renderedFilters()).toMatchObject({ page: 1 });
  });

  it('FR-SRCH-002/003/004: đọc bộ lọc từ URL vào cả form lẫn danh sách', async () => {
    renderWithProviders(
      await RecipesPage(
        props({ categoryId: 'c1', difficulty: 'Hard', maxCookTime: '45', sort: '-title', page: '2' }),
      ),
    );

    expect(renderedFilters()).toMatchObject({
      categoryId: 'c1',
      difficulty: 'Hard',
      maxCookTime: '45',
      sort: '-title',
      page: 2,
    });
    expect(screen.getByRole('combobox', { name: 'Độ khó' })).toHaveValue('Hard');
    expect(screen.getByRole('spinbutton', { name: 'Nấu tối đa (phút)' })).toHaveValue(45);
  });

  it('FR-SRCH-004: page không hợp lệ → 1', async () => {
    renderWithProviders(await RecipesPage(props({ page: 'abc' })));

    expect(renderedFilters()).toMatchObject({ page: 1 });
  });

  it('FR-RCP-001: API danh mục lỗi → trang vẫn hiển thị, bộ lọc chỉ còn "Tất cả"', async () => {
    mockGetCategories.mockRejectedValue(new Error('down'));

    renderWithProviders(await RecipesPage(props()));

    expect(screen.getByRole('combobox', { name: 'Danh mục' })).toHaveTextContent('Tất cả');
    expect(screen.getByTestId('results')).toBeInTheDocument();
  });
});
