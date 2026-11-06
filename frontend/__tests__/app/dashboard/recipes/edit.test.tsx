import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EditRecipePage from '@/app/dashboard/recipes/[id]/edit/page';
import { apiClient } from '@/lib/api-client';
import type { CategoryDto } from '@/lib/types';
import { problemError } from '../../../utils/problem';
import { recipeDetail } from '../../../utils/recipe';
import { renderWithProviders } from '../../../utils/render';

jest.mock('next/navigation', () => ({
  useParams: () => ({ id: 'r1' }),
  useRouter: () => ({ push: jest.fn(), replace: jest.fn(), back: jest.fn(), prefetch: jest.fn() }),
  usePathname: () => '/dashboard/recipes/r1/edit',
}));

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), patch: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

// Component ảnh của D (FR-RCP-008) đã có test riêng — ở đây chỉ kiểm tra trang truyền đúng dữ liệu.
jest.mock('@/components/upload/RecipeImageManager', () => ({
  RecipeImageManager: ({ recipeId, initialImages }: { recipeId: string; initialImages: { imageId: string }[] }) => (
    <p data-testid="image-manager">
      {recipeId}:{initialImages.map((i) => i.imageId).join(',')}
    </p>
  ),
}));

const categories: CategoryDto[] = [{ id: 'c1', name: 'Món nước', slug: 'mon-nuoc', description: null, recipeCount: 1 }];

function mockGet(recipeResult: unknown) {
  api.get.mockImplementation(((url: string) => {
    if (url === '/categories') return Promise.resolve({ data: categories });
    if (recipeResult instanceof Error) return Promise.reject(recipeResult);
    return Promise.resolve({ data: recipeResult });
  }) as never);
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('/dashboard/recipes/[id]/edit — FR-RCP-004/005/007/009/010 (D75)', () => {
  it('NFR-USE-004: đang tải → skeleton', () => {
    api.get.mockReturnValue(new Promise(() => {}) as never);
    renderWithProviders(<EditRecipePage />);

    expect(screen.getByLabelText('Đang tải công thức')).toBeInTheDocument();
  });

  it('D75: đọc GET /recipes/{id} → hiện thông tin chung, nguyên liệu, các bước, ảnh', async () => {
    mockGet(recipeDetail({ id: 'r1', status: 0 }));
    renderWithProviders(<EditRecipePage />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Sửa công thức' })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/recipes/r1');
    expect(screen.getByText('Nháp')).toBeInTheDocument();
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Phở bò Hà Nội');
    expect(screen.getByRole('heading', { name: 'Nguyên liệu (3)' })).toBeInTheDocument();
    expect(screen.getByText('vừa đủ')).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Các bước thực hiện' })).toBeInTheDocument();
    expect(screen.getByTestId('image-manager')).toHaveTextContent('r1:img1,img2');
    expect(screen.getByRole('link', { name: /Xem trang công thức/ })).toHaveAttribute('href', '/recipes/pho-bo-ha-noi');
  });

  it('NFR-SEC-006: không phải chủ sở hữu (403 RECIPE_FORBIDDEN) → thông báo, không hiện form', async () => {
    mockGet(problemError({ type: 'RECIPE_FORBIDDEN', title: 'Không có quyền', status: 403 }));
    renderWithProviders(<EditRecipePage />);

    expect(await screen.findByRole('heading', { name: 'Bạn không có quyền sửa công thức này' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Tiêu đề')).not.toBeInTheDocument();
  });

  it('404 RECIPE_NOT_FOUND → "Không tìm thấy công thức"', async () => {
    mockGet(problemError({ type: 'RECIPE_NOT_FOUND', title: 'Không tìm thấy', status: 404 }));
    renderWithProviders(<EditRecipePage />);

    expect(await screen.findByRole('heading', { name: 'Không tìm thấy công thức' })).toBeInTheDocument();
  });
  it('FR-RCP-005/D77: Xuất bản → badge "Đã xuất bản"; lần lưu thông tin chung sau đó gửi If-Match = rowVersion MỚI', async () => {
    const draft = recipeDetail({ id: 'r1', status: 0, rowVersion: 'AQIDBA==' });
    mockGet(draft);
    api.patch.mockResolvedValue({ data: { ...draft, status: 1, rowVersion: 'BQYHCA==' } } as never);
    api.put.mockResolvedValue({ data: { ...draft, status: 1, title: 'Phở bò Nam Định', rowVersion: 'CQoLDA==' } } as never);
    const user = userEvent.setup();
    renderWithProviders(<EditRecipePage />);

    await user.click(await screen.findByRole('button', { name: 'Xuất bản' }));
    expect(await screen.findByText('Đã xuất bản', { selector: 'span' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Hủy xuất bản' })).toBeInTheDocument();

    const title = screen.getByLabelText('Tiêu đề');
    await user.clear(title);
    await user.type(title, 'Phở bò Nam Định');
    await user.click(screen.getByRole('button', { name: 'Lưu thông tin chung' }));

    await waitFor(() => expect(api.put).toHaveBeenCalled());
    expect(api.put.mock.calls[0][2]).toEqual({ headers: { 'If-Match': '"BQYHCA=="' } });
  });

  it('FR-RCP-009: phần nguyên liệu có nút thêm / sửa / xóa', async () => {
    mockGet(recipeDetail({ id: 'r1', status: 0 }));
    renderWithProviders(<EditRecipePage />);

    expect(await screen.findByRole('list', { name: 'Danh sách nguyên liệu' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Thêm nguyên liệu' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sửa xương bò' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa công thức' })).toBeInTheDocument();
  });
});
