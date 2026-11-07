import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { revalidateRecipeAction } from '@/app/dashboard/recipes/actions';
import { RecipePublishControls } from '@/components/recipes/editor/RecipePublishControls';
import { apiClient } from '@/lib/api-client';
import { RecipeStatus, type RecipeDetailDto } from '@/lib/types';
import { problemError } from '../../utils/problem';
import { recipeDetail } from '../../utils/recipe';
import { renderWithProviders } from '../../utils/render';

const push = jest.fn();
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: jest.fn(), back: jest.fn(), prefetch: jest.fn() }),
}));

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), patch: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

jest.mock('@/app/dashboard/recipes/actions', () => ({ revalidateRecipeAction: jest.fn() }));
const revalidate = revalidateRecipeAction as jest.MockedFunction<typeof revalidateRecipeAction>;

function setup(recipe: RecipeDetailDto) {
  const user = userEvent.setup();
  const onChange = jest.fn();
  renderWithProviders(<RecipePublishControls recipe={recipe} onChange={onChange} />);
  return { user, onChange };
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('RecipePublishControls — FR-RCP-005 xuất bản / FR-RCP-006 lưu trữ / FR-RCP-007 xóa', () => {
  it('FR-RCP-005: Draft đủ điều kiện → Xuất bản → PATCH publish, trả DTO mới (rowVersion mới) cho trang cha', async () => {
    const published = recipeDetail({ id: 'r1', status: RecipeStatus.Published, rowVersion: 'BQYHCA==' });
    api.patch.mockResolvedValue({ data: published } as never);
    const { user, onChange } = setup(recipeDetail({ id: 'r1', status: RecipeStatus.Draft }));

    await user.click(screen.getByRole('button', { name: 'Xuất bản' }));

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(published));
    expect(api.patch).toHaveBeenCalledWith('/recipes/r1/publish');
    expect(await screen.findByText('Đã xuất bản')).toBeInTheDocument();
  });

  it('D3: thiếu nguyên liệu và bước → nút Xuất bản bị khóa, nói rõ thiếu gì', () => {
    setup(recipeDetail({ status: RecipeStatus.Draft, ingredients: [], steps: [] }));

    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeDisabled();
    expect(screen.getByText('Cần thêm ít nhất 1 nguyên liệu và 1 bước thực hiện để xuất bản.')).toBeInTheDocument();
  });

  it('FR-RCP-005: Published → Hủy xuất bản → PATCH unpublish', async () => {
    const draft = recipeDetail({ id: 'r1', status: RecipeStatus.Draft });
    api.patch.mockResolvedValue({ data: draft } as never);
    const { user, onChange } = setup(recipeDetail({ id: 'r1', status: RecipeStatus.Published }));

    expect(screen.queryByRole('button', { name: 'Xuất bản' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Hủy xuất bản' }));

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(draft));
    expect(api.patch).toHaveBeenCalledWith('/recipes/r1/unpublish');
    expect(await screen.findByText('Đã chuyển về nháp')).toBeInTheDocument();
  });

  it('D77: Archived → có cả Xuất bản và Chuyển về nháp', async () => {
    api.patch.mockResolvedValue({ data: recipeDetail({ id: 'r1', status: RecipeStatus.Draft }) } as never);
    const { user } = setup(recipeDetail({ id: 'r1', status: RecipeStatus.Archived }));

    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'Chuyển về nháp' }));

    await waitFor(() => expect(api.patch).toHaveBeenCalledWith('/recipes/r1/unpublish'));
  });

  it('D3: server từ chối publish (RECIPE_PUBLISH_INCOMPLETE) → toast lỗi, không gọi onChange', async () => {
    api.patch.mockRejectedValue(problemError({ type: 'RECIPE_PUBLISH_INCOMPLETE', title: 'Thiếu dữ liệu', status: 400 }));
    const { user, onChange } = setup(recipeDetail({ status: RecipeStatus.Draft }));

    await user.click(screen.getByRole('button', { name: 'Xuất bản' }));

    expect(await screen.findByText('Không đổi được trạng thái')).toBeInTheDocument();
    expect(screen.getByText(/Công thức cần ít nhất 1 nguyên liệu và 1 bước thực hiện/)).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
  });

  it.each([RecipeStatus.Draft, RecipeStatus.Published])(
    'FR-RCP-006/D79: status %s → Lưu trữ → hộp thoại xác nhận → PATCH archive, trả DTO mới cho trang cha',
    async (status) => {
      const archived = recipeDetail({ id: 'r1', status: RecipeStatus.Archived, rowVersion: 'CQoLDA==' });
      api.patch.mockResolvedValue({ data: archived } as never);
      const { user, onChange } = setup(recipeDetail({ id: 'r1', status }));

      await user.click(screen.getByRole('button', { name: 'Lưu trữ' }));
      const dialog = screen.getByRole('dialog', { name: 'Lưu trữ công thức này?' });
      await user.click(within(dialog).getByRole('button', { name: 'Lưu trữ' }));

      await waitFor(() => expect(onChange).toHaveBeenCalledWith(archived));
      expect(api.patch).toHaveBeenCalledWith('/recipes/r1/archive');
      expect(await screen.findByText('Đã lưu trữ')).toBeInTheDocument();
    },
  );

  it('FR-RCP-006/D77: recipe đã Archived → không có nút Lưu trữ, giải thích cách thoát khỏi lưu trữ', () => {
    setup(recipeDetail({ status: RecipeStatus.Archived }));

    expect(screen.queryByRole('button', { name: 'Lưu trữ' })).not.toBeInTheDocument();
    expect(screen.getByText(/Công thức đang được lưu trữ/)).toBeInTheDocument();
  });

  it('FR-RCP-006: recipe chưa Archived → không hiện dòng giải thích lưu trữ', () => {
    setup(recipeDetail({ status: RecipeStatus.Published }));

    expect(screen.queryByText(/Công thức đang được lưu trữ/)).not.toBeInTheDocument();
  });

  it('FR-RCP-006: hủy hộp thoại lưu trữ → không gọi API', async () => {
    const { user } = setup(recipeDetail({ id: 'r1' }));

    await user.click(screen.getByRole('button', { name: 'Lưu trữ' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Hủy' }));

    expect(api.patch).not.toHaveBeenCalled();
  });

  it('FR-RCP-006/D79: lưu trữ lỗi 409 → toast lỗi, không gọi onChange', async () => {
    api.patch.mockRejectedValue(problemError({ type: 'RECIPE_CONCURRENCY_CONFLICT', title: 'Xung đột', status: 409 }));
    const { user, onChange } = setup(recipeDetail({ id: 'r1' }));

    await user.click(screen.getByRole('button', { name: 'Lưu trữ' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Lưu trữ' }));

    expect(await screen.findByText('Không lưu trữ được công thức')).toBeInTheDocument();
    expect(screen.getByText(/Công thức vừa được thay đổi ở nơi khác/)).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
  });

  it('FR-RCP-007: Xóa → hộp thoại xác nhận → DELETE → toast, về /dashboard', async () => {
    api.delete.mockResolvedValue({ data: undefined } as never);
    const { user } = setup(recipeDetail({ id: 'r1' }));

    await user.click(screen.getByRole('button', { name: 'Xóa công thức' }));
    const dialog = screen.getByRole('dialog', { name: 'Xóa công thức này?' });
    expect(within(dialog).getByText(/\/recipes\/pho-bo-ha-noi sẽ không dùng lại được/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Xóa công thức' }));

    await waitFor(() => expect(push).toHaveBeenCalledWith('/dashboard'));
    expect(api.delete).toHaveBeenCalledWith('/recipes/r1');
    expect(await screen.findByText('Đã xóa công thức')).toBeInTheDocument();
  });

  it('FR-RCP-007: hủy hộp thoại → không gọi API', async () => {
    const { user } = setup(recipeDetail({ id: 'r1' }));

    await user.click(screen.getByRole('button', { name: 'Xóa công thức' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Hủy' }));

    expect(api.delete).not.toHaveBeenCalled();
  });

  it('FR-RCP-007: xóa lỗi 403 → toast lỗi, ở lại trang', async () => {
    api.delete.mockRejectedValue(problemError({ type: 'RECIPE_FORBIDDEN', title: 'Không có quyền', status: 403 }));
    const { user } = setup(recipeDetail({ id: 'r1' }));

    await user.click(screen.getByRole('button', { name: 'Xóa công thức' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xóa công thức' }));

    expect(await screen.findByText('Không xóa được công thức')).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  describe('D80 — xóa cache trang công khai sau khi đổi trạng thái', () => {
    it.each([
      ['Xuất bản', RecipeStatus.Draft, undefined],
      ['Hủy xuất bản', RecipeStatus.Published, undefined],
      ['Lưu trữ', RecipeStatus.Draft, 'Lưu trữ công thức này?'],
    ])('%s thành công → gọi revalidateRecipeAction(slug)', async (button, status, dialogName) => {
      api.patch.mockResolvedValue({ data: recipeDetail({ id: 'r1', slug: 'pho-bo-ha-noi' }) } as never);
      const { user, onChange } = setup(recipeDetail({ id: 'r1', slug: 'pho-bo-ha-noi', status }));

      await user.click(screen.getByRole('button', { name: button }));
      if (dialogName) {
        await user.click(within(screen.getByRole('dialog', { name: dialogName })).getByRole('button', { name: button }));
      }

      await waitFor(() => expect(onChange).toHaveBeenCalled());
      expect(revalidate).toHaveBeenCalledWith('pho-bo-ha-noi');
    });

    it('Xóa thành công → gọi revalidateRecipeAction(slug) rồi mới về /dashboard', async () => {
      api.delete.mockResolvedValue({ data: undefined } as never);
      const { user } = setup(recipeDetail({ id: 'r1', slug: 'pho-bo-ha-noi' }));

      await user.click(screen.getByRole('button', { name: 'Xóa công thức' }));
      await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xóa công thức' }));

      await waitFor(() => expect(push).toHaveBeenCalledWith('/dashboard'));
      expect(revalidate).toHaveBeenCalledWith('pho-bo-ha-noi');
    });

    it('API lỗi → không gọi revalidateRecipeAction', async () => {
      api.patch.mockRejectedValue(problemError({ type: 'RECIPE_FORBIDDEN', title: 'Không có quyền', status: 403 }));
      const { user } = setup(recipeDetail({ status: RecipeStatus.Draft }));

      await user.click(screen.getByRole('button', { name: 'Xuất bản' }));

      expect(await screen.findByText('Không đổi được trạng thái')).toBeInTheDocument();
      expect(revalidate).not.toHaveBeenCalled();
    });

    it('revalidateRecipeAction lỗi → thao tác vẫn thành công (toast thành công, onChange được gọi)', async () => {
      revalidate.mockRejectedValue(new Error('network'));
      const archived = recipeDetail({ id: 'r1', status: RecipeStatus.Archived });
      api.patch.mockResolvedValue({ data: archived } as never);
      const { user, onChange } = setup(recipeDetail({ id: 'r1' }));

      await user.click(screen.getByRole('button', { name: 'Lưu trữ' }));
      await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Lưu trữ' }));

      await waitFor(() => expect(onChange).toHaveBeenCalledWith(archived));
      expect(await screen.findByText('Đã lưu trữ')).toBeInTheDocument();
    });
  });
});
