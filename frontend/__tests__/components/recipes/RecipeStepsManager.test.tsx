import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeStepsManager } from '@/components/recipes/editor/RecipeStepsManager';
import { apiClient } from '@/lib/api-client';
import type { RecipeStepDto } from '@/lib/types';
import { problemError } from '../../utils/problem';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

const steps: RecipeStepDto[] = [
  { id: 's2', stepNumber: 2, title: 'Ninh nước dùng', description: 'Ninh 8 tiếng', timerMinutes: 480 },
  { id: 's1', stepNumber: 1, title: 'Sơ chế xương', description: 'Chần xương' },
  { id: 's3', stepNumber: 3, title: 'Trụng bánh', description: 'Trụng bánh phở' },
];

function titlesInOrder() {
  const list = screen.getByRole('list', { name: 'Các bước thực hiện' });
  return within(list)
    .getAllByRole('heading', { level: 3 })
    .map((h) => h.textContent);
}

function setup() {
  const user = userEvent.setup();
  const onChange = jest.fn();
  renderWithProviders(<RecipeStepsManager recipeId="r1" initialSteps={steps} onChange={onChange} />);
  return { user, onChange };
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('RecipeStepsManager — FR-RCP-010 quản lý bước trên trang sửa', () => {
  it('hiển thị theo StepNumber tăng dần', () => {
    setup();

    expect(titlesInOrder()).toEqual(['Bước 1: Sơ chế xương', 'Bước 2: Ninh nước dùng', 'Bước 3: Trụng bánh']);
    expect(screen.getByText('480 phút')).toBeInTheDocument();
  });

  it('D6: thêm bước → POST không có stepNumber, bước mới nằm cuối, toast', async () => {
    api.post.mockResolvedValue({
      data: { id: 's4', stepNumber: 4, title: 'Chan nước', description: 'Chan nước dùng', timerMinutes: 2 },
    } as never);
    const { user, onChange } = setup();

    await user.click(screen.getByRole('button', { name: 'Thêm bước' }));
    await user.type(screen.getByLabelText('Bước mới — tiêu đề'), 'Chan nước');
    await user.type(screen.getByLabelText('Bước mới — mô tả'), 'Chan nước dùng');
    await user.type(screen.getByLabelText('Bước mới — hẹn giờ (phút, tùy chọn)'), '2');
    await user.click(screen.getByRole('button', { name: 'Thêm bước' }));

    await waitFor(() => expect(titlesInOrder()).toHaveLength(4));
    expect(api.post).toHaveBeenCalledWith('/recipes/r1/steps', {
      title: 'Chan nước',
      description: 'Chan nước dùng',
      timerMinutes: 2,
    });
    expect(titlesInOrder()[3]).toBe('Bước 4: Chan nước');
    expect(onChange).toHaveBeenLastCalledWith(expect.arrayContaining([expect.objectContaining({ id: 's4' })]));
    expect(await screen.findByText('Đã thêm bước')).toBeInTheDocument();
  });

  it('D6/D73: nút Lên → PUT kèm stepNumber mới, danh sách theo server trả về', async () => {
    api.put.mockResolvedValue({
      data: [
        { id: 's3', stepNumber: 1, title: 'Trụng bánh', description: 'Trụng bánh phở' },
        { id: 's1', stepNumber: 2, title: 'Sơ chế xương', description: 'Chần xương' },
        { id: 's2', stepNumber: 3, title: 'Ninh nước dùng', description: 'Ninh 8 tiếng', timerMinutes: 480 },
      ],
    } as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Chuyển bước 3 lên' }));

    expect(api.put).toHaveBeenCalledWith('/recipes/r1/steps/s3', {
      title: 'Trụng bánh',
      description: 'Trụng bánh phở',
      timerMinutes: null,
      stepNumber: 2,
    });
    await waitFor(() =>
      expect(titlesInOrder()).toEqual(['Bước 1: Trụng bánh', 'Bước 2: Sơ chế xương', 'Bước 3: Ninh nước dùng']),
    );
  });

  it('optimistic + rollback: đổi vị trí lỗi → về thứ tự cũ, toast lỗi theo error code', async () => {
    let reject!: (e: unknown) => void;
    api.put.mockReturnValue(new Promise((_, r) => (reject = r)) as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Chuyển bước 2 xuống' }));

    // Cập nhật lạc quan: đổi ngay trước khi server trả lời.
    expect(titlesInOrder()).toEqual(['Bước 1: Sơ chế xương', 'Bước 2: Trụng bánh', 'Bước 3: Ninh nước dùng']);
    reject(problemError({ type: 'RECIPE_FORBIDDEN', title: 'Không có quyền', status: 403 }));

    await waitFor(() =>
      expect(titlesInOrder()).toEqual(['Bước 1: Sơ chế xương', 'Bước 2: Ninh nước dùng', 'Bước 3: Trụng bánh']),
    );
    expect(await screen.findByText('Bạn không có quyền sửa công thức này.')).toBeInTheDocument();
  });

  it('xóa: hỏi xác nhận → xóa ngay trên giao diện + đánh số lại, gọi DELETE', async () => {
    api.delete.mockResolvedValue({ data: undefined } as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Xóa bước 1' }));
    const dialog = screen.getByRole('dialog', { name: 'Xóa bước này?' });
    await user.click(within(dialog).getByRole('button', { name: 'Xóa bước' }));

    expect(titlesInOrder()).toEqual(['Bước 1: Ninh nước dùng', 'Bước 2: Trụng bánh']);
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('/recipes/r1/steps/s1'));
    expect(await screen.findByText('Đã xóa bước')).toBeInTheDocument();
  });

  it('D73: xóa bước cuối của recipe đã xuất bản bị server chặn → khôi phục bước, báo lý do', async () => {
    api.delete.mockRejectedValue(
      problemError({ type: 'RECIPE_PUBLISH_INCOMPLETE', title: 'Vi phạm quy tắc nghiệp vụ', status: 400 }),
    );
    const user = userEvent.setup();
    renderWithProviders(<RecipeStepsManager recipeId="r1" initialSteps={[steps[1]]} />);

    await user.click(screen.getByRole('button', { name: 'Xóa bước 1' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xóa bước' }));

    expect(await screen.findByText(/Công thức cần ít nhất 1 nguyên liệu và 1 bước thực hiện/)).toBeInTheDocument();
    expect(titlesInOrder()).toEqual(['Bước 1: Sơ chế xương']);
  });

  it('sửa bước tại chỗ → PUT nội dung (không stepNumber), hủy thì không gọi API', async () => {
    api.put.mockResolvedValue({
      data: [
        { id: 's1', stepNumber: 1, title: 'Sơ chế xương bò', description: 'Chần xương 5 phút', timerMinutes: 5 },
        steps[0],
        steps[2],
      ],
    } as never);
    const { user } = setup();

    await user.click(screen.getByRole('button', { name: 'Sửa bước 2' }));
    await user.click(screen.getByRole('button', { name: 'Hủy' }));
    expect(api.put).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Sửa bước 1' }));
    await user.clear(screen.getByLabelText('Bước 1 — tiêu đề'));
    await user.type(screen.getByLabelText('Bước 1 — tiêu đề'), 'Sơ chế xương bò');
    await user.clear(screen.getByLabelText('Bước 1 — mô tả'));
    await user.type(screen.getByLabelText('Bước 1 — mô tả'), 'Chần xương 5 phút');
    await user.type(screen.getByLabelText('Bước 1 — hẹn giờ (phút, tùy chọn)'), '5');
    await user.click(screen.getByRole('button', { name: 'Lưu bước' }));

    await waitFor(() => expect(titlesInOrder()[0]).toBe('Bước 1: Sơ chế xương bò'));
    expect(api.put).toHaveBeenCalledWith('/recipes/r1/steps/s1', {
      title: 'Sơ chế xương bò',
      description: 'Chần xương 5 phút',
      timerMinutes: 5,
    });
  });
});
