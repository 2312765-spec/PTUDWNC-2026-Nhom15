import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeBasicsForm } from '@/components/recipes/editor/RecipeBasicsForm';
import { apiClient } from '@/lib/api-client';
import type { CategoryDto } from '@/lib/types';
import { problemError } from '../../utils/problem';
import { recipeDetail } from '../../utils/recipe';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

const categories: CategoryDto[] = [
  { id: 'c1', name: 'Món nước', slug: 'mon-nuoc', description: null, recipeCount: 1 },
  { id: 'c2', name: 'Món cuốn', slug: 'mon-cuon', description: null, recipeCount: 0 },
];

const recipe = recipeDetail({ id: 'r1', rowVersion: 'djE=', instructions: 'Ăn nóng' });

function setup(onReload = jest.fn()) {
  const user = userEvent.setup();
  const onSaved = jest.fn();
  renderWithProviders(<RecipeBasicsForm recipe={recipe} onSaved={onSaved} onReload={onReload} />);
  return { user, onSaved, onReload };
}

beforeEach(() => {
  jest.resetAllMocks();
  api.get.mockResolvedValue({ data: categories } as never);
});

describe('RecipeBasicsForm — FR-RCP-004 sửa thông tin chung', () => {
  it('hiện sẵn dữ liệu hiện tại, chưa sửa thì nút Lưu bị khóa', async () => {
    setup();

    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Phở bò Hà Nội');
    expect(await screen.findByLabelText('Danh mục')).toHaveValue('c1');
    expect(screen.getByLabelText('Nấu (phút)')).toHaveValue('480');
    expect(screen.getByRole('button', { name: 'Lưu thông tin chung' })).toBeDisabled();
  });

  it('D71: lưu → PUT /recipes/{id} kèm If-Match "<rowVersion>", toast, báo DTO mới lên trang', async () => {
    const updated = recipeDetail({ id: 'r1', title: 'Phở bò tái', rowVersion: 'djI=' });
    api.put.mockResolvedValue({ data: updated } as never);
    const { user, onSaved } = setup();
    await screen.findByLabelText('Danh mục');

    await user.clear(screen.getByLabelText('Tiêu đề'));
    await user.type(screen.getByLabelText('Tiêu đề'), 'Phở bò tái');
    await user.click(screen.getByRole('button', { name: 'Lưu thông tin chung' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(api.put).toHaveBeenCalledWith(
      '/recipes/r1',
      expect.objectContaining({ title: 'Phở bò tái', categoryId: 'c1', cookTime: 480, instructions: 'Ăn nóng' }),
      { headers: { 'If-Match': '"djE="' } },
    );
    expect(await screen.findByText('Đã lưu thay đổi')).toBeInTheDocument();
  });

  it('FR-RCP-004/A2: 409 RECIPE_CONCURRENCY_CONFLICT → báo xung đột, "Tải lại bản mới nhất" nạp lại form', async () => {
    api.put.mockRejectedValue(
      problemError({ type: 'RECIPE_CONCURRENCY_CONFLICT', title: 'Xung đột dữ liệu', status: 409 }),
    );
    const latest = recipeDetail({ id: 'r1', title: 'Bản người khác vừa lưu', rowVersion: 'djM=' });
    const onReload = jest.fn().mockResolvedValue(latest);
    const { user } = setup(onReload);
    await screen.findByLabelText('Danh mục');

    await user.type(screen.getByLabelText('Tiêu đề'), ' sửa');
    await user.click(screen.getByRole('button', { name: 'Lưu thông tin chung' }));

    expect(await screen.findByText(/vừa được thay đổi ở nơi khác/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Tải lại bản mới nhất' }));

    await waitFor(() => expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Bản người khác vừa lưu'));
    expect(onReload).toHaveBeenCalled();
    expect(screen.queryByText(/vừa được thay đổi ở nơi khác/)).not.toBeInTheDocument();
  });

  it('NFR-USE-003: lỗi validation của server gắn vào đúng ô', async () => {
    api.put.mockRejectedValue(
      problemError({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        errors: { CategoryId: ['Danh mục không hợp lệ.'] },
      }),
    );
    const { user } = setup();
    await screen.findByLabelText('Danh mục');

    await user.selectOptions(screen.getByLabelText('Danh mục'), 'c2');
    await user.click(screen.getByRole('button', { name: 'Lưu thông tin chung' }));

    expect(await screen.findByText('Danh mục không hợp lệ.')).toBeInTheDocument();
  });

  it('client validation: tiêu đề quá ngắn → không gọi API', async () => {
    const { user } = setup();
    await screen.findByLabelText('Danh mục');

    await user.clear(screen.getByLabelText('Tiêu đề'));
    await user.type(screen.getByLabelText('Tiêu đề'), 'Phở');
    await user.click(screen.getByRole('button', { name: 'Lưu thông tin chung' }));

    expect(await screen.findByText('Tiêu đề phải từ 5 đến 200 ký tự.')).toBeInTheDocument();
    expect(api.put).not.toHaveBeenCalled();
  });
});
