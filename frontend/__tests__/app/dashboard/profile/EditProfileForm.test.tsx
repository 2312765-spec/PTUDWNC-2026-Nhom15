import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProfilePage from '@/app/dashboard/profile/page';
import { apiClient } from '@/lib/api-client';
import type { UserProfile } from '@/lib/types';
import { problemError } from '../../../utils/problem';
import { renderWithProviders } from '../../../utils/render';

const mockUpdateSession = jest.fn();
jest.mock('next-auth/react', () => ({
  useSession: () => ({ data: null, status: 'authenticated', update: mockUpdateSession }),
}));

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), patch: jest.fn() },
}));

const api = apiClient as jest.Mocked<typeof apiClient>;

const lan: UserProfile = {
  id: 'u1',
  email: 'lan@example.com',
  displayName: 'Bếp của Lan',
  avatarUrl: 'https://example.com/a.png',
  bio: 'Thích nấu món Huế',
  roles: ['Author'],
};

async function openForm() {
  const user = userEvent.setup();
  api.get.mockResolvedValueOnce({ data: lan } as never);
  renderWithProviders(<ProfilePage />);
  await user.click(await screen.findByRole('button', { name: 'Chỉnh sửa hồ sơ' }));
  return user;
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('EditProfileForm — FR-AUTH-007', () => {
  it('mở form với giá trị hiện tại của hồ sơ', async () => {
    await openForm();

    expect(screen.getByLabelText('Tên hiển thị')).toHaveValue('Bếp của Lan');
    expect(screen.getByLabelText(/Đường dẫn ảnh đại diện/)).toHaveValue('https://example.com/a.png');
    expect(screen.getByLabelText(/Giới thiệu/)).toHaveValue('Thích nấu món Huế');
  });

  it('D5/D51: gửi PATCH /auth/me với đủ 3 field, hiện toast, quay về chế độ xem với dữ liệu mới', async () => {
    const updated = { ...lan, displayName: 'Bếp Mới' };
    api.patch.mockResolvedValueOnce({ data: updated } as never);
    const user = await openForm();

    const name = screen.getByLabelText('Tên hiển thị');
    await user.clear(name);
    await user.type(name, 'Bếp Mới');
    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    await waitFor(() =>
      expect(api.patch).toHaveBeenCalledWith('/auth/me', {
        displayName: 'Bếp Mới',
        avatarUrl: 'https://example.com/a.png',
        bio: 'Thích nấu món Huế',
      }),
    );
    expect(await screen.findByText('Đã cập nhật hồ sơ')).toBeInTheDocument(); // NFR-USE-004
    expect(await screen.findByRole('heading', { name: 'Bếp Mới' })).toBeInTheDocument();
    expect(mockUpdateSession).toHaveBeenCalledWith({ profile: updated }); // header đồng bộ
  });

  it('D51-2: để trống avatarUrl và bio → gửi chuỗi rỗng (xóa), không phải bỏ field', async () => {
    api.patch.mockResolvedValueOnce({ data: { ...lan, avatarUrl: null, bio: null } } as never);
    const user = await openForm();

    await user.clear(screen.getByLabelText(/Đường dẫn ảnh đại diện/));
    await user.clear(screen.getByLabelText(/Giới thiệu/));
    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    await waitFor(() =>
      expect(api.patch).toHaveBeenCalledWith('/auth/me', {
        displayName: 'Bếp của Lan',
        avatarUrl: '',
        bio: '',
      }),
    );
  });

  it('D5: displayName 1 ký tự → lỗi inline, không gọi API', async () => {
    const user = await openForm();

    const name = screen.getByLabelText('Tên hiển thị');
    await user.clear(name);
    await user.type(name, 'A');
    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    expect(await screen.findByText('Tên hiển thị cần ít nhất 2 ký tự.')).toBeInTheDocument();
    expect(api.patch).not.toHaveBeenCalled();
  });

  it('D51-3: avatarUrl không phải http/https → lỗi inline, không gọi API', async () => {
    const user = await openForm();

    const avatar = screen.getByLabelText(/Đường dẫn ảnh đại diện/);
    await user.clear(avatar);
    await user.type(avatar, 'javascript:alert(1)');
    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    expect(await screen.findByText(/phải bắt đầu bằng http/)).toBeInTheDocument();
    expect(api.patch).not.toHaveBeenCalled();
  });

  it('D4: backend trả 400 theo field → hiện lỗi dưới đúng ô, vẫn ở trong form', async () => {
    api.patch.mockRejectedValueOnce(
      problemError({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        errors: { bio: ['Giới thiệu quá dài.'] },
      }),
    );
    const user = await openForm();

    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    expect(await screen.findByText('Giới thiệu quá dài.')).toBeInTheDocument();
    expect(screen.getByRole('form', { name: 'Chỉnh sửa hồ sơ' })).toBeInTheDocument();
  });

  it('lỗi không gắn được vào field (404 USER_NOT_FOUND) → thông báo chung, không đóng form', async () => {
    api.patch.mockRejectedValueOnce(
      problemError({ type: 'USER_NOT_FOUND', title: 'Không tìm thấy', status: 404, detail: 'Không tìm thấy người dùng.' }),
    );
    const user = await openForm();

    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Không tìm thấy người dùng.');
    expect(screen.getByRole('form', { name: 'Chỉnh sửa hồ sơ' })).toBeInTheDocument();
  });

  it('đồng bộ session thất bại không biến thành lỗi lưu — vẫn toast và đóng form', async () => {
    mockUpdateSession.mockRejectedValueOnce(new Error('session down'));
    api.patch.mockResolvedValueOnce({ data: lan } as never);
    const user = await openForm();

    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    expect(await screen.findByText('Đã cập nhật hồ sơ')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('form', { name: 'Chỉnh sửa hồ sơ' })).not.toBeInTheDocument());
  });

  it('nút Hủy quay về chế độ xem, không gọi API', async () => {
    const user = await openForm();

    await user.click(screen.getByRole('button', { name: 'Hủy' }));

    expect(screen.getByRole('button', { name: 'Chỉnh sửa hồ sơ' })).toBeInTheDocument();
    expect(api.patch).not.toHaveBeenCalled();
  });
});
