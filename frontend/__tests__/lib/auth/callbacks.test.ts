/**
 * @jest-environment node
 */
import type { Session, User } from 'next-auth';
import type { JWT } from 'next-auth/jwt';
import { jwtCallback, sessionCallback } from '@/lib/auth/callbacks';
import { isAccessTokenExpiring, refreshAccessToken } from '@/lib/auth/refresh';

jest.mock('@/lib/auth/refresh', () => ({
  isAccessTokenExpiring: jest.fn(),
  refreshAccessToken: jest.fn(),
}));
const mockExpiring = isAccessTokenExpiring as jest.MockedFunction<typeof isAccessTokenExpiring>;
const mockRefreshAccessToken = refreshAccessToken as jest.MockedFunction<typeof refreshAccessToken>;

const profile = {
  id: 'u1',
  email: 'lan@example.com',
  displayName: 'Bếp của Lan',
  avatarUrl: null,
  bio: null,
  roles: ['Author'],
};

const token: JWT = {
  profile,
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  expiresAt: '2026-09-30T10:15:00Z',
};

beforeEach(() => {
  mockExpiring.mockReset();
  mockRefreshAccessToken.mockReset();
});

describe('jwtCallback — FR-AUTH-004', () => {
  it('FR-AUTH-001/002/003: vừa đăng nhập (có user) → chép token backend vào JWT, không refresh', async () => {
    const user: User = {
      id: 'u1',
      profile,
      accessToken: 'access-login',
      refreshToken: 'refresh-login',
      expiresAt: '2026-09-30T10:15:00Z',
    };

    const result = await jwtCallback({ token: {}, user });

    expect(result).toMatchObject({
      profile,
      accessToken: 'access-login',
      refreshToken: 'refresh-login',
      expiresAt: '2026-09-30T10:15:00Z',
    });
    expect(mockRefreshAccessToken).not.toHaveBeenCalled();
  });

  it('FR-AUTH-004: access token còn hạn → trả token nguyên, không gọi backend', async () => {
    mockExpiring.mockReturnValue(false);

    const result = await jwtCallback({ token });

    expect(mockExpiring).toHaveBeenCalledWith(token.expiresAt);
    expect(result).toBe(token);
    expect(mockRefreshAccessToken).not.toHaveBeenCalled();
  });

  it('FR-AUTH-004: access token sắp hết hạn → trả kết quả của refreshAccessToken', async () => {
    const refreshed: JWT = { ...token, accessToken: 'access-2', refreshToken: 'refresh-2' };
    mockExpiring.mockReturnValue(true);
    mockRefreshAccessToken.mockResolvedValue(refreshed);

    const result = await jwtCallback({ token });

    expect(mockRefreshAccessToken).toHaveBeenCalledWith(token);
    expect(result).toBe(refreshed);
  });

  it('FR-AUTH-004/D35: token đã mang RefreshTokenError → không thử refresh lại (tránh gọi backend liên tục)', async () => {
    const failed: JWT = { profile, error: 'RefreshTokenError' };
    mockExpiring.mockReturnValue(true);

    const result = await jwtCallback({ token: failed });

    expect(result).toBe(failed);
    expect(mockRefreshAccessToken).not.toHaveBeenCalled();
  });

  it('FR-AUTH-004: phiên không đăng nhập (token rỗng, không có refreshToken) → không gọi refresh', async () => {
    mockExpiring.mockReturnValue(true);

    const result = await jwtCallback({ token: {} });

    expect(result).toEqual({});
    expect(mockRefreshAccessToken).not.toHaveBeenCalled();
  });
});

describe('sessionCallback — FR-AUTH-004', () => {
  const baseSession: Session = { expires: '2026-10-07T10:00:00Z' };

  it('D5/D24: đưa profile + accessToken xuống client', () => {
    const session = sessionCallback({ session: { ...baseSession }, token });

    expect(session.profile).toEqual(profile);
    expect(session.accessToken).toBe('access-1');
    expect(session.error).toBeUndefined();
  });

  it('FR-AUTH-004: đưa error ra client để SessionExpiryWatcher đăng xuất', () => {
    const session = sessionCallback({
      session: { ...baseSession },
      token: { profile, error: 'RefreshTokenError' },
    });

    expect(session.error).toBe('RefreshTokenError');
  });

  it('FR-AUTH-004/NFR-SEC-002: KHÔNG bao giờ để refreshToken lọt xuống client', () => {
    const session = sessionCallback({ session: { ...baseSession }, token });

    expect(JSON.stringify(session)).not.toContain('refresh-1');
    expect(session).not.toHaveProperty('refreshToken');
  });
});
