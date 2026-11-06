import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeIngredientsManager } from '@/components/recipes/editor/RecipeIngredientsManager';
import { apiClient } from '@/lib/api-client';
import type { RecipeIngredientDto } from '@/lib/types';
import { problemError } from '../../utils/problem';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

const ingredients: RecipeIngredientDto[] = [
  { id: 'i2', name: 'Bánh phở', quantity: 500, unit: 'g', orderIndex: 1 },
  { id: 'i1', name: 'Xương bò', quantity: 1.5, unit: 'kg', notes: 'chần sơ', orderIndex: 0 },
  { id: 'i3', name: 'Hành lá', orderIndex: 2 },
];

function namesInOrder() {
  const list = screen.getByRole('list', { name: 'Danh sách nguyên liệu' });
  return within(list)
    .getAllByRole('listitem')
    .map((li) => li.querySelector('.font-medium')?.textContent);
}

function setup(initial = ingredients) {
  const user = userEvent.setup();
  const onChange = jest.fn();
  renderWithProviders(<RecipeIngredientsManager recipeId="r1" initialIngredients={initial} onChange={onChange} />);
  return { user, onChange };
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('RecipeIngredientsManager — FR-RCP-009 quản lý nguyên liệu trên trang sửa', () => {
  it('hiển thị theo orderIndex tăng dần, số lượng trống = "vừa đủ" (D7)', () => {
    setup();

    expect(namesInOrder()).toEqual(['Xương bò', 'Bánh phở', 'Hành lá']);
    expect(screen.getByText('1,5 kg')).toBeInTheDocument();
    expect(screen.getByText('chần sơ')).toBeInTheDocument();
    expect(screen.getByText('vừa đủ')).toBeInTheDocument();
  });

  it('danh sách rỗng → nhắc cần ít nhất 1 nguyên liệu để xuất bản (D3)', () => {
    setup([]);

    expect(screen.getByText(/Cần ít nhất 1 nguyên liệu để xuất bản/)).toBeInTheDocument();
  });

  it('D7/D76: thêm nguyên liệu → POST (dấu phẩy thập phân, ô trống = null), xếp cuối, toast', async () => {
    api.post.mockResolvedValue({
      data: { id: 'i4', name: 'Gừng', quantity: 0.5, unit: 'củ', orderIndex: 3 },
    } as never);
    const { user, onChange } = setup();

    await user.click(screen.getByRole('button', { name: 'Thêm nguyên liệu' }));
    await user.type(screen.getByLabelText('Nguyên liệu mới — tên'), ' Gừng ');
    await user.type(screen.getByLabelText('Nguyên liệu mới — số lượng'), '0,5');
    await user.type(screen.getByLabelText('Nguyên liệu mới — đơn vị'), 'củ');
    await user.click(screen.getByRole('button', { name: 'Thêm nguyên liệu' }));

    await waitFor(() => expect(namesInOrder()).toHaveLength(4));
    expect(api.post).toHaveBeenCalledWith('/recipes/r1/ingredients', { name: 'Gừng', quantity: 0.5, unit: 'củ', notes: null });
    expect(namesInOrder()[3]).toBe('Gừng');
    expect(onChange).toHaveBeenLastCalledWith(expect.arrayContaining([expect.objectContaining({ id: 'i4' })]));
    expect(await screen.findByText('Đã thêm nguyên liệu')).toBeInTheDocument();
  });

  it('validate phía client: tên trống, số lượng 0 → báo lỗi, không gọi API', async () => {
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Thêm nguyên liệu' }));
    await user.type(screen.getByLabelText('Nguyên liệu mới — số lượng'), '0');
    await user.click(screen.getByRole('button', { name: 'Thêm nguyên liệu' }));

    expect(await screen.findByText('Nhập tên nguyên liệu.')).toBeInTheDocument();
    expect(screen.getByText('Số lượng phải lớn hơn 0.')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('D76: nút Lên → PUT kèm orderIndex mới (0-based), danh sách theo server trả về', async () => {
    api.put.mockResolvedValue({
      data: [
        { id: 'i3', name: 'Hành lá', orderIndex: 0 },
        { id: 'i1', name: 'Xương bò', quantity: 1.5, unit: 'kg', notes: 'chần sơ', orderIndex: 1 },
        { id: 'i2', name: 'Bánh phở', quantity: 500, unit: 'g', orderIndex: 2 },
      ],
    } as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Chuyển Hành lá lên' }));

    expect(api.put).toHaveBeenCalledWith('/recipes/r1/ingredients/i3', {
      name: 'Hành lá',
      quantity: null,
      unit: null,
      notes: null,
      orderIndex: 1,
    });
    await waitFor(() => expect(namesInOrder()).toEqual(['Hành lá', 'Xương bò', 'Bánh phở']));
  });

  it('optimistic + rollback: đổi vị trí lỗi → về thứ tự cũ, toast lỗi theo error code', async () => {
    let reject!: (e: unknown) => void;
    api.put.mockReturnValue(new Promise((_, r) => (reject = r)) as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Chuyển Xương bò xuống' }));

    expect(namesInOrder()).toEqual(['Bánh phở', 'Xương bò', 'Hành lá']);
    reject(problemError({ type: 'RECIPE_INGREDIENT_NOT_FOUND', title: 'Không tìm thấy', status: 404 }));

    await waitFor(() => expect(namesInOrder()).toEqual(['Xương bò', 'Bánh phở', 'Hành lá']));
    expect(await screen.findByText('Nguyên liệu này không còn tồn tại. Hãy tải lại trang.')).toBeInTheDocument();
  });

  it('sửa tại chỗ → PUT nội dung (không orderIndex); xóa trắng số lượng = "vừa đủ"', async () => {
    api.put.mockResolvedValue({
      data: [
        { id: 'i1', name: 'Xương ống bò', orderIndex: 0 },
        ingredients[0],
        ingredients[2],
      ],
    } as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Sửa Xương bò' }));
    const name = screen.getByLabelText('Nguyên liệu 1 — tên');
    expect(screen.getByLabelText('Nguyên liệu 1 — số lượng')).toHaveValue('1,5');
    await user.clear(name);
    await user.type(name, 'Xương ống bò');
    await user.clear(screen.getByLabelText('Nguyên liệu 1 — số lượng'));
    await user.clear(screen.getByLabelText('Nguyên liệu 1 — đơn vị'));
    await user.clear(screen.getByLabelText('Nguyên liệu 1 — ghi chú (tùy chọn)'));
    await user.click(screen.getByRole('button', { name: 'Lưu nguyên liệu' }));

    await waitFor(() => expect(namesInOrder()[0]).toBe('Xương ống bò'));
    expect(api.put).toHaveBeenCalledWith('/recipes/r1/ingredients/i1', {
      name: 'Xương ống bò',
      quantity: null,
      unit: null,
      notes: null,
    });
  });

  it('xóa: hỏi xác nhận → xóa ngay trên giao diện, gọi DELETE, toast', async () => {
    api.delete.mockResolvedValue({ data: undefined } as never);
    const { user, onChange } = setup();

    await user.click(screen.getByRole('button', { name: 'Xóa Xương bò' }));
    const dialog = screen.getByRole('dialog', { name: 'Xóa nguyên liệu này?' });
    await user.click(within(dialog).getByRole('button', { name: 'Xóa nguyên liệu' }));

    expect(namesInOrder()).toEqual(['Bánh phở', 'Hành lá']);
    expect(onChange).toHaveBeenLastCalledWith([
      expect.objectContaining({ id: 'i2', orderIndex: 0 }),
      expect.objectContaining({ id: 'i3', orderIndex: 1 }),
    ]);
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('/recipes/r1/ingredients/i1'));
    expect(await screen.findByText('Đã xóa nguyên liệu')).toBeInTheDocument();
  });

  it('D3/D76: xóa nguyên liệu cuối của recipe đã xuất bản bị server chặn → khôi phục, báo lý do', async () => {
    api.delete.mockRejectedValue(
      problemError({ type: 'RECIPE_PUBLISH_INCOMPLETE', title: 'Vi phạm quy tắc nghiệp vụ', status: 400 }),
    );
    const { user } = setup([ingredients[1]]);

    await user.click(screen.getByRole('button', { name: 'Xóa Xương bò' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xóa nguyên liệu' }));

    expect(await screen.findByText(/Công thức cần ít nhất 1 nguyên liệu và 1 bước thực hiện/)).toBeInTheDocument();
    expect(namesInOrder()).toEqual(['Xương bò']);
  });
});
