import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeWizard } from '@/components/recipes/editor/RecipeWizard';
import { apiClient } from '@/lib/api-client';
import type { CategoryDto } from '@/lib/types';
import { mockPush } from '../../../jest.setup';
import { problemError } from '../../utils/problem';
import { recipeDetail } from '../../utils/recipe';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

const categories: CategoryDto[] = [
  { id: 'c1', name: 'Món cuốn', slug: 'mon-cuon', description: null, recipeCount: 0 },
  { id: 'c2', name: 'Món nước', slug: 'mon-nuoc', description: null, recipeCount: 2 },
];

type User = ReturnType<typeof userEvent.setup>;

async function fillBasics(user: User) {
  await user.type(screen.getByLabelText('Tiêu đề'), 'Gỏi cuốn tôm thịt');
  await user.type(screen.getByLabelText('Mô tả ngắn'), 'Món cuốn tươi mát');
  await user.selectOptions(await screen.findByLabelText('Danh mục'), 'c1');
  await user.type(screen.getByLabelText('Chuẩn bị (phút)'), '20');
}

async function goToReview(user: User) {
  await fillBasics(user);
  await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
  await user.type(await screen.findByLabelText('Tên nguyên liệu 1'), 'Tôm');
  await user.type(screen.getByLabelText('Số lượng'), '300');
  await user.type(screen.getByLabelText('Đơn vị'), 'g');
  await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
  await user.type(await screen.findByLabelText('Tiêu đề bước 1'), 'Sơ chế');
  await user.type(screen.getByLabelText('Mô tả'), 'Luộc tôm');
  await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
  await screen.findByRole('heading', { name: /Xem lại/ });
}

beforeEach(() => {
  jest.resetAllMocks();
  api.get.mockResolvedValue({ data: categories } as never);
});

describe('RecipeWizard — FR-RCP-003 tạo công thức nhiều bước', () => {
  it('bước 1 thiếu dữ liệu → không sang bước 2, hiện lỗi inline', async () => {
    const user = userEvent.setup();
    renderWithProviders(<RecipeWizard />);

    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));

    expect(await screen.findByText('Tiêu đề phải từ 5 đến 200 ký tự.')).toBeInTheDocument();
    expect(screen.getByText('Hãy chọn danh mục.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /Bước 1\/4/ })).toBeInTheDocument();
  });

  it('NFR-USE-002: thanh tiến trình đánh dấu bước hiện tại (aria-current="step")', async () => {
    const user = userEvent.setup();
    renderWithProviders(<RecipeWizard />);
    await fillBasics(user);

    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));

    const nav = screen.getByRole('navigation', { name: 'Các bước tạo công thức' });
    expect(within(nav).getByText('Nguyên liệu').closest('li')).toHaveAttribute('aria-current', 'step');
    await waitFor(() => expect(screen.getByRole('heading', { name: /Bước 2\/4: Nguyên liệu/ })).toHaveFocus());
  });

  it('hồi quy: bấm Tiếp tục ở bước 3 sang "Xem lại" → KHÔNG tự gửi form (từng tạo trùng 2 công thức)', async () => {
    const user = userEvent.setup();
    renderWithProviders(<RecipeWizard />);
    await fillBasics(user);

    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
    await user.click(await screen.findByRole('button', { name: 'Xóa nguyên liệu 1' }));
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
    await user.click(await screen.findByRole('button', { name: 'Xóa bước 1' }));
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));

    expect(await screen.findByRole('heading', { name: /Xem lại/ })).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('đi hết 4 bước → POST /recipes đúng body (D69), toast, chuyển sang trang sửa', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue({ data: recipeDetail({ id: 'r-new', title: 'Gỏi cuốn tôm thịt' }) } as never);
    renderWithProviders(<RecipeWizard />);
    await goToReview(user);

    const review = screen.getByRole('heading', { name: /Xem lại/ }).closest('section')!;
    expect(within(review).getByText('Gỏi cuốn tôm thịt', { selector: 'dd' })).toBeInTheDocument();
    expect(within(review).getByText('Món cuốn', { selector: 'dd' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Tạo công thức' }));

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith('/dashboard/recipes/r-new/edit'));
    expect(api.post).toHaveBeenCalledWith('/recipes', {
      title: 'Gỏi cuốn tôm thịt',
      description: 'Món cuốn tươi mát',
      categoryId: 'c1',
      prepTime: 20,
      cookTime: 0,
      servings: 4,
      difficulty: 1,
      instructions: null,
      ingredients: [{ name: 'Tôm', quantity: 300, unit: 'g', notes: null }],
      steps: [{ title: 'Sơ chế', description: 'Luộc tôm', timerMinutes: null }],
    });
    expect(await screen.findByText('Đã tạo công thức')).toBeInTheDocument();
  });

  it('D6: đổi thứ tự bước bằng nút Lên → thứ tự gửi lên đổi theo', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue({ data: recipeDetail({ id: 'r2' }) } as never);
    renderWithProviders(<RecipeWizard />);
    await fillBasics(user);
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
    await user.click(await screen.findByRole('button', { name: 'Xóa nguyên liệu 1' }));
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
    await user.type(await screen.findByLabelText('Tiêu đề bước 1'), 'Một');
    await user.type(screen.getAllByLabelText('Mô tả')[0], 'Bước một');
    await user.click(screen.getByRole('button', { name: 'Thêm bước' }));
    await user.type(screen.getByLabelText('Tiêu đề bước 2'), 'Hai');
    await user.type(screen.getAllByLabelText('Mô tả')[1], 'Bước hai');

    await user.click(screen.getByRole('button', { name: 'Chuyển bước 2 lên' }));
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));
    await user.click(await screen.findByRole('button', { name: 'Tạo công thức' }));

    await waitFor(() => expect(api.post).toHaveBeenCalled());
    const body = api.post.mock.calls[0][1] as { steps: { title: string }[]; ingredients: unknown[] };
    expect(body.steps.map((s) => s.title)).toEqual(['Hai', 'Một']);
    expect(body.ingredients).toEqual([]);
  });

  it('NFR-USE-003: server trả lỗi Steps[0].Title → quay về bước "Các bước", lỗi gắn đúng ô', async () => {
    const user = userEvent.setup();
    api.post.mockRejectedValue(
      problemError({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        errors: { 'Steps[0].Title': ['Tiêu đề bước không được để trống.'] },
      }),
    );
    renderWithProviders(<RecipeWizard />);
    await goToReview(user);

    await user.click(screen.getByRole('button', { name: 'Tạo công thức' }));

    expect(await screen.findByText('Tiêu đề bước không được để trống.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /Bước 3\/4: Các bước/ })).toBeInTheDocument();
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('lỗi không gắn được vào ô (403) → thông báo chung, không chuyển trang', async () => {
    const user = userEvent.setup();
    api.post.mockRejectedValue(problemError({ type: 'RECIPE_FORBIDDEN', title: 'Không có quyền', status: 403 }));
    renderWithProviders(<RecipeWizard />);
    await goToReview(user);

    await user.click(screen.getByRole('button', { name: 'Tạo công thức' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền sửa công thức này.');
    expect(mockPush).not.toHaveBeenCalled();
  });
});
