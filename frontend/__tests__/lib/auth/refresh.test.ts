/**
 * @jest-environment node
 */
import type { JWT } from 'next-auth/jwt';
import { refresh } from '@/lib/auth/api';
import {
  isAccessTokenExpiring,
  REFRESH_RESULT_TTL_MS,
  REFRESH_SKEW_MS,
  refreshAccessToken,
} from '@/lib/auth/refresh';
import type { AuthResponse } from '@/lib/types';
import { networkError, problemError } from '../../utils/problem';

jest.mock('@/lib/auth/api', () => ({ refresh: jest.fn() }));
const mockRefresh = refresh as jest.MockedFunction<typeof refresh>;

const NOW = Date.parse('2026-09-30T10:00:00Z');

const profile = {
  id: 'u1',
  email: 'lan@example.com',
  displayName: 'Bếp của Lan',
  avatarUrl: null,
  bio: null,
  roles: ['Author'],
};

/** Mỗi test một RT riêng — kết quả refresh được giữ theo RT (gộp trùng), không để test giẫm nhau. */
let seq = 0;
function tokenWith(overrides: Partial<JWT> = {}): JWT {
  seq += 1;
  return {
    profile,
    accessToken: `old-access-${seq}`,
    refreshToken: `old-refresh-${seq}`,
    expiresAt: new Date(NOW - 1_000).toISOString(),
    ...overrides,
  };
}

function authResponse(n = 1): AuthResponse {
  return {
    accessToken: `new-access-${n}`,
    refreshToken: `new-refresh-${n}`,
    expiresAt: new Date(NOW + 15 * 60_000).toISOString(),
    user: { ...profile, displayName: 'Bếp của Lan (mới)' },
  };
}

const problem = (status: number, type: string) => problemError({ type, title: type, status });

beforeEach(() => {
  mockRefresh.mockReset();
  jest.spyOn(Date, 'now').mockReturnValue(NOW);
});

afterEach(() => {
  jest.restoreAllMocks();
});

describe('isAccessTokenExpiring — FR-AUTH-004', () => {
  it('FR-AUTH-004: còn hạn lâu hơn khoảng đệm → chưa cần refresh', () => {
    const expiresAt = new Date(NOW + REFRESH_SKEW_MS + 1_000).toISOString();
    expect(isAccessTokenExpiring(expiresAt)).toBe(false);
  });

  it('FR-AUTH-004: còn hạn ít hơn khoảng đệm (60s) → refresh trước khi hết hạn thật', () => {
    const expiresAt = new Date(NOW + REFRESH_SKEW_MS - 1_000).toISOString();
    expect(isAccessTokenExpiring(expiresAt)).toBe(true);
  });

  it('FR-AUTH-004: đã hết hạn → cần refresh', () => {
    expect(isAccessTokenExpiring(new Date(NOW - 1).toISOString())).toBe(true);
  });

  it('FR-AUTH-004: không có hoặc sai định dạng expiresAt → coi như hết hạn', () => {
    expect(isAccessTokenExpiring(undefined)).toBe(true);
    expect(isAccessTokenExpiring('không-phải-ngày')).toBe(true);
  });
});

describe('refreshAccessToken — FR-AUTH-004', () => {
  it('FR-AUTH-004/D24: thành công → thay accessToken, refreshToken, expiresAt, profile bằng giá trị mới', async () => {
    const token = tokenWith();
    mockRefresh.mockResolvedValue(authResponse());

    const result = await refreshAccessToken(token);

    expect(mockRefresh).toHaveBeenCalledWith({ refreshToken: token.refreshToken });
    expect(result).toMatchObject({
      accessToken: 'new-access-1',
      refreshToken: 'new-refresh-1',
      expiresAt: authResponse().expiresAt,
      profile: { displayName: 'Bếp của Lan (mới)' },
    });
    expect(result.error).toBeUndefined();
  });

  it.each([
    [401, 'AUTH_REFRESH_TOKEN_REVOKED'],
    [401, 'AUTH_REFRESH_TOKEN_EXPIRED'],
    [401, 'AUTH_TOKEN_INVALID'],
    [403, 'AUTH_ACCOUNT_DISABLED'],
  ])(
    'FR-AUTH-004/D35: backend trả %i %s → error RefreshTokenError, bỏ token cũ',
    async (status, type) => {
      mockRefresh.mockRejectedValue(problem(status, type));

      const result = await refreshAccessToken(tokenWith());

      expect(result.error).toBe('RefreshTokenError');
      expect(result.refreshToken).toBeUndefined();
      expect(result.accessToken).toBeUndefined();
    },
  );

  it('FR-AUTH-004: lỗi mạng (backend không phản hồi) → giữ token nguyên, KHÔNG đăng xuất user', async () => {
    const token = tokenWith();
    mockRefresh.mockRejectedValue(networkError());

    const result = await refreshAccessToken(token);

    expect(result).toEqual(token);
    expect(result.error).toBeUndefined();
  });

  it('FR-AUTH-004: backend 5xx → giữ token nguyên, request sau thử lại', async () => {
    const token = tokenWith();
    mockRefresh.mockRejectedValue(problem(500, 'INTERNAL_ERROR'));

    const result = await refreshAccessToken(token);

    expect(result).toEqual(token);
  });

  it('FR-AUTH-004: token không có refreshToken → RefreshTokenError, không gọi backend', async () => {
    const result = await refreshAccessToken(tokenWith({ refreshToken: undefined }));

    expect(mockRefresh).not.toHaveBeenCalled();
    expect(result.error).toBe('RefreshTokenError');
  });

  it('FR-AUTH-004/D35: 2 lần refresh ĐỒNG THỜI cùng một RT → backend chỉ bị gọi 1 lần (tránh reuse detection)', async () => {
    const token = tokenWith();
    let resolve!: (value: AuthResponse) => void;
    mockRefresh.mockReturnValue(new Promise<AuthResponse>((r) => (resolve = r)));

    const first = refreshAccessToken(token);
    const second = refreshAccessToken({ ...token });
    resolve(authResponse());

    const [a, b] = await Promise.all([first, second]);
    expect(mockRefresh).toHaveBeenCalledTimes(1);
    expect(a.refreshToken).toBe('new-refresh-1');
    expect(b.refreshToken).toBe('new-refresh-1');
  });

  it('FR-AUTH-004/D35: request đến SAU khi rotation xong nhưng còn mang cookie cũ → dùng lại kết quả, không gửi RT cũ lên lần nữa', async () => {
    const token = tokenWith();
    mockRefresh.mockResolvedValue(authResponse());

    await refreshAccessToken(token);
    jest.spyOn(Date, 'now').mockReturnValue(NOW + REFRESH_RESULT_TTL_MS - 1_000);
    const later = await refreshAccessToken({ ...token });

    expect(mockRefresh).toHaveBeenCalledTimes(1);
    expect(later.refreshToken).toBe('new-refresh-1');
  });

  it('FR-AUTH-004: kết quả cũ hơn thời gian giữ → gọi backend lại (không giữ mãi trong bộ nhớ)', async () => {
    const token = tokenWith();
    mockRefresh.mockResolvedValueOnce(authResponse(1)).mockResolvedValueOnce(authResponse(2));

    await refreshAccessToken(token);
    jest.spyOn(Date, 'now').mockReturnValue(NOW + REFRESH_RESULT_TTL_MS + 1_000);
    const later = await refreshAccessToken({ ...token });

    expect(mockRefresh).toHaveBeenCalledTimes(2);
    expect(later.refreshToken).toBe('new-refresh-2');
  });

  it('FR-AUTH-004: lần refresh lỗi mạng KHÔNG được giữ lại → lần sau gọi backend thật', async () => {
    const token = tokenWith();
    mockRefresh.mockRejectedValueOnce(networkError()).mockResolvedValueOnce(authResponse());

    await refreshAccessToken(token);
    const retried = await refreshAccessToken({ ...token });

    expect(mockRefresh).toHaveBeenCalledTimes(2);
    expect(retried.refreshToken).toBe('new-refresh-1');
  });
});
