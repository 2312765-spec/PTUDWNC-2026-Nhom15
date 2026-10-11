import type { JWT } from 'next-auth/jwt';
import { logout } from '@/lib/auth/api';
import { revokeRefreshToken } from '@/lib/auth/logout';
import { refreshAccessToken } from '@/lib/auth/refresh';

jest.mock('@/lib/auth/api', () => ({ logout: jest.fn() }));
jest.mock('@/lib/auth/refresh', () => ({
  ...jest.requireActual('@/lib/auth/refresh'),
  refreshAccessToken: jest.fn(),
}));

const mockLogout = logout as jest.Mock;
const mockRefresh = refreshAccessToken as jest.Mock;

const future = () => new Date(Date.now() + 10 * 60_000).toISOString();
const past = () => new Date(Date.now() - 60_000).toISOString();

function tokenWith(overrides: Partial<JWT> = {}): JWT {
  return { accessToken: 'at', refreshToken: 'rt', expiresAt: future(), ...overrides } as JWT;
}

beforeEach(() => {
  jest.resetAllMocks();
  mockLogout.mockResolvedValue(undefined);
  jest.spyOn(console, 'warn').mockImplementation(() => undefined);
});

describe('revokeRefreshToken — FR-AUTH-005', () => {
  it('FR-AUTH-005: access token còn hạn → gọi /auth/logout với RT và access token hiện có', async () => {
    await revokeRefreshToken(tokenWith());

    expect(mockRefresh).not.toHaveBeenCalled();
    expect(mockLogout).toHaveBeenCalledWith({ refreshToken: 'rt' }, 'at');
  });

  it('FR-AUTH-005: không có refresh token → không gọi backend', async () => {
    await revokeRefreshToken(tokenWith({ refreshToken: undefined }));

    expect(mockLogout).not.toHaveBeenCalled();
  });

  it('FR-AUTH-005/FR-AUTH-004: phiên đã hỏng (RefreshTokenError) → không gọi backend', async () => {
    await revokeRefreshToken(tokenWith({ error: 'RefreshTokenError' }));

    expect(mockLogout).not.toHaveBeenCalled();
  });

  it('FR-AUTH-005/D47-1: access token sắp hết hạn → refresh trước, logout bằng cặp token mới', async () => {
    mockRefresh.mockResolvedValue(tokenWith({ accessToken: 'at2', refreshToken: 'rt2' }));

    await revokeRefreshToken(tokenWith({ expiresAt: past() }));

    expect(mockLogout).toHaveBeenCalledWith({ refreshToken: 'rt2' }, 'at2');
  });

  it('FR-AUTH-005: refresh trước khi logout bị từ chối → không gọi logout, không ném', async () => {
    mockRefresh.mockResolvedValue(
      tokenWith({ accessToken: undefined, refreshToken: undefined, error: 'RefreshTokenError' }),
    );

    await expect(revokeRefreshToken(tokenWith({ expiresAt: past() }))).resolves.toBeUndefined();
    expect(mockLogout).not.toHaveBeenCalled();
  });

  it('FR-AUTH-005: lỗi mạng / 5xx từ backend → không ném (người dùng vẫn đăng xuất được)', async () => {
    mockLogout.mockRejectedValue(new Error('Network Error'));

    await expect(revokeRefreshToken(tokenWith())).resolves.toBeUndefined();
  });
});
