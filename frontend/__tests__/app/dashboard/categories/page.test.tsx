import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AdminCategoriesPage from '@/app/dashboard/categories/page';
import { apiClient } from '@/lib/api-client';
import type { CategoryDto } from '@/lib/types';
import { problemError } from '../../../utils/problem';
import { renderWithProviders } from '../../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));

const api = apiClient as jest.Mocked<typeof apiClient>;

const pho: CategoryDto = { id: 'c1', name: 'Món nước', slug: 'mon-nuoc', description: 'Phở, bún', recipeCount: 3 };
const banh: CategoryDto = { id: 'c2', name: 'Bánh ngọt', slug: 'banh-ngot', description: null, recipeCount: 0 };

function mockList(...lists: CategoryDto[][]) {
  for (const list of lists) {
    api.get.mockResolvedValueOnce({ data: list } as never);
  }
}

function setup() {
  const user = userEvent.setup();
  renderWithProviders(<AdminCategoriesPage />);
  return { user };
}

async function openCreateForm(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByText('Món nước');
  await user.click(screen.getByRole('button', { name: '+ Thêm danh mục' }));
  return screen.getByRole('dialog', { name: 'Tạo danh mục mới' });
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('AdminCategoriesPage — danh sách (FR-CAT-001)', () => {
  it('NFR-USE-004: hiện skeleton khi đang tải, sau đó hiện bảng', async () => {
    mockList([pho, banh]);
    setup();

    expect(screen.getByLabelText('Đang tải danh sách danh mục')).toBeInTheDocument();
    expect(await screen.findByText('Món nước')).toBeInTheDocument();
    expect(screen.getByText('banh-ngot')).toBeInTheDocument();
  });

  it('danh sách rỗng → thông báo chưa có danh mục', async () => {
    mockList([]);
    setup();

    expect(await screen.findByText('Chưa có danh mục nào.')).toBeInTheDocument();
  });

  it('lỗi tải → thông báo lỗi + nút thử lại', async () => {
    api.get.mockRejectedValueOnce(
      problemError({ type: 'INTERNAL_ERROR', title: 'Lỗi', status: 500, detail: 'Máy chủ lỗi.' }),
    );
    setup();

    expect(await screen.findByText('Máy chủ lỗi.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
  });
});

describe('AdminCategoriesPage — tạo mới (FR-CAT-003)', () => {
  it('tạo thành công → POST đúng body, toast, tải lại danh sách', async () => {
    mockList([pho], [pho, banh]);
    api.post.mockResolvedValueOnce({ data: banh } as never);
    const { user } = setup();

    const dialog = await openCreateForm(user);
    await user.type(within(dialog).getByLabelText('Tên danh mục'), '  Bánh ngọt ');
    await user.click(within(dialog).getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/categories', { name: 'Bánh ngọt', description: null }),
    );
    expect(await screen.findByText('Đã tạo danh mục')).toBeInTheDocument();
    expect(await screen.findByText('banh-ngot')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('validate phía client: tên 1 ký tự → báo lỗi, không gọi API', async () => {
    mockList([pho]);
    const { user } = setup();

    const dialog = await openCreateForm(user);
    await user.type(within(dialog).getByLabelText('Tên danh mục'), 'A');
    await user.click(within(dialog).getByRole('button', { name: 'Tạo mới' }));

    expect(await within(dialog).findByText('Tên danh mục phải từ 2 đến 50 ký tự.')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('NFR-USE-003: lỗi validation backend key "Name" (PascalCase) → hiện inline ở ô tên', async () => {
    mockList([pho]);
    api.post.mockRejectedValueOnce(
      problemError({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        errors: { Name: ['Tên danh mục không được chứa mã HTML.'] },
      }),
    );
    const { user } = setup();

    const dialog = await openCreateForm(user);
    await user.type(within(dialog).getByLabelText('Tên danh mục'), '<b>Bánh</b>');
    await user.click(within(dialog).getByRole('button', { name: 'Tạo mới' }));

    expect(await within(dialog).findByText('Tên danh mục không được chứa mã HTML.')).toBeInTheDocument();
  });

  it('409 CATEGORY_NAME_EXISTS → báo trùng tên ở ô tên', async () => {
    mockList([pho]);
    api.post.mockRejectedValueOnce(
      problemError({ type: 'CATEGORY_NAME_EXISTS', title: 'Trùng', status: 409 }),
    );
    const { user } = setup();

    const dialog = await openCreateForm(user);
    await user.type(within(dialog).getByLabelText('Tên danh mục'), 'Món nước');
    await user.click(within(dialog).getByRole('button', { name: 'Tạo mới' }));

    expect(await within(dialog).findByText('Tên danh mục này đã tồn tại.')).toBeInTheDocument();
  });
});

describe('AdminCategoriesPage — sửa (FR-CAT-004)', () => {
  it('form điền sẵn dữ liệu cũ, lưu → PUT /categories/{id}', async () => {
    mockList([pho], [pho]);
    api.put.mockResolvedValueOnce({ data: pho } as never);
    const { user } = setup();

    await screen.findByText('Món nước');
    await user.click(screen.getByRole('button', { name: 'Sửa danh mục Món nước' }));
    const dialog = screen.getByRole('dialog', { name: 'Chỉnh sửa danh mục' });

    const name = within(dialog).getByLabelText('Tên danh mục');
    expect(name).toHaveValue('Món nước');
    expect(within(dialog).getByLabelText('Mô tả (tùy chọn)')).toHaveValue('Phở, bún');

    await user.clear(name);
    await user.type(name, 'Món nước Việt');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu thay đổi' }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith('/categories/c1', { name: 'Món nước Việt', description: 'Phở, bún' }),
    );
    expect(await screen.findByText('Đã cập nhật danh mục')).toBeInTheDocument();
  });
});
