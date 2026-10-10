import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RecipeFilters from '@/app/(public)/recipes/RecipeFilters';
import type { CategoryDto } from '@/lib/types';
import { mockPush } from '../../../jest.setup';
import { renderWithProviders } from '../../utils/render';

const categories: CategoryDto[] = [
  { id: 'c1', name: 'Món chay', slug: 'mon-chay', description: null, recipeCount: 3 },
];

describe('RecipeFilters — FR-SRCH-002/003', () => {
  it('WCAG 2.1 AA: mọi ô lọc có nhãn; độ khó có Expert (D14)', () => {
    renderWithProviders(<RecipeFilters initial={{}} categories={categories} />);

    expect(screen.getByRole('combobox', { name: 'Danh mục' })).toBeInTheDocument();
    expect(screen.getByRole('spinbutton', { name: 'Nấu tối đa (phút)' })).toBeInTheDocument();
    expect(screen.getByRole('spinbutton', { name: 'Khẩu phần tối thiểu' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Sắp xếp' })).toHaveValue('-createdAt');
    expect(screen.getByRole('option', { name: 'Rất khó' })).toHaveAttribute('value', 'Expert');
  });

  it('FR-SRCH-002/003/004: áp dụng → URL chỉ chứa filter đã chọn, quay về trang 1', async () => {
    renderWithProviders(
      <RecipeFilters initial={{ page: 4 }} categories={categories} />,
    );

    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Danh mục' }), 'c1');
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Độ khó' }), 'Hard');
    await userEvent.type(screen.getByRole('spinbutton', { name: 'Khẩu phần tối thiểu' }), '4');
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Sắp xếp' }), '-cookTime');
    await userEvent.click(screen.getByRole('button', { name: 'Áp dụng' }));

    expect(mockPush).toHaveBeenCalledWith(
      '/recipes?categoryId=c1&difficulty=Hard&minServings=4&sort=-cookTime',
    );
  });

  it('FR-SRCH-002: không chọn gì → /recipes (URL gọn)', async () => {
    renderWithProviders(<RecipeFilters initial={{}} categories={[]} />);

    await userEvent.click(screen.getByRole('button', { name: 'Áp dụng' }));

    expect(mockPush).toHaveBeenCalledWith('/recipes');
  });

  it('FR-SRCH-002: đang có bộ lọc → hiện link "Xóa bộ lọc" về /recipes', () => {
    renderWithProviders(<RecipeFilters initial={{ difficulty: 'Easy' }} categories={[]} />);

    expect(screen.getByRole('link', { name: 'Xóa bộ lọc' })).toHaveAttribute('href', '/recipes');
  });
});
