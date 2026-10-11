import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProfilePage from '@/app/dashboard/profile/page';
import { apiClient } from '@/lib/api-client';
import type { UserProfile } from '@/lib/types';
import { problemError } from '../../../utils/problem';
import { renderWithProviders } from '../../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn() },
}));

const api = apiClient as jest.Mocked<typeof apiClient>;

const lan: UserProfile = {
  id: 'u1',
  email: 'lan@example.com',
  displayName: 'Bếp của Lan',
  avatarUrl: null,
  bio: 'Thích nấu món Huế',
  roles: ['Author'],
};

beforeEach(() => {
  jest.resetAllMocks();
});

describe('ProfilePage — FR-AUTH-006', () => {
  it('NFR-USE-004: hiện skeleton khi đang tải, sau đó hiện hồ sơ', async () => {
    api.get.mockResolvedValueOnce({ data: lan } as never);
    renderWithProviders(<ProfilePage />);

    expect(screen.getByLabelText('Đang tải hồ sơ')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Bếp của Lan' })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/auth/me');
  });

  it('D5: hiển thị email, bio và vai trò', async () => {
    api.get.mockResolvedValueOnce({ data: { ...lan, roles: ['Admin'] } } as never);
    renderWithProviders(<ProfilePage />);

    expect(await screen.findByText('lan@example.com')).toBeInTheDocument();
    expect(screen.getByText('Thích nấu món Huế')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('bio null → hiện "Chưa có giới thiệu"', async () => {
    api.get.mockResolvedValueOnce({ data: { ...lan, bio: null } } as never);
    renderWithProviders(<ProfilePage />);

    expect(await screen.findByText('Chưa có giới thiệu')).toBeInTheDocument();
  });

  it('avatarUrl null → avatar chữ cái đầu', async () => {
    api.get.mockResolvedValueOnce({ data: lan } as never);
    renderWithProviders(<ProfilePage />);

    expect(await screen.findByTestId('avatar-fallback')).toHaveTextContent('B');
  });

  it('có avatarUrl → thẻ img có alt', async () => {
    api.get.mockResolvedValueOnce({ data: { ...lan, avatarUrl: 'https://example.com/a.png' } } as never);
    renderWithProviders(<ProfilePage />);

    expect(await screen.findByAltText('Ảnh đại diện của Bếp của Lan')).toHaveAttribute(
      'src',
      'https://example.com/a.png',
    );
  });

  it('lỗi tải → thông báo lỗi + nút thử lại gọi lại API', async () => {
    api.get
      .mockRejectedValueOnce(
        problemError({ type: 'USER_NOT_FOUND', title: 'Không tìm thấy', status: 404, detail: 'Không tìm thấy người dùng.' }),
      )
      .mockResolvedValueOnce({ data: lan } as never);
    const user = userEvent.setup();
    renderWithProviders(<ProfilePage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Không tìm thấy người dùng.');
    await user.click(screen.getByRole('button', { name: 'Thử lại' }));

    expect(await screen.findByRole('heading', { name: 'Bếp của Lan' })).toBeInTheDocument();
  });
});
