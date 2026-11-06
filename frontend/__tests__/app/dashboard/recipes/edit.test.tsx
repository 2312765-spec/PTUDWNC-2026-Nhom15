import { screen } from '@testing-library/react';
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
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
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

describe('/dashboard/recipes/[id]/edit — FR-RCP-004 + FR-RCP-010 (D75)', () => {
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
});
