import { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { getSession } from 'next-auth/react';
import { attachAccessToken, getBrowserAccessToken, resetAccessTokenCache } from '@/lib/api-client';

jest.mock('next-auth/react', () => ({ getSession: jest.fn() }));
const mockGetSession = getSession as jest.Mock;

function config(headers: Record<string, string> = {}): InternalAxiosRequestConfig {
  return { headers: new AxiosHeaders(headers) } as InternalAxiosRequestConfig;
}

beforeEach(() => {
  jest.resetAllMocks();
  resetAccessTokenCache();
});

describe('apiClient — D74: gắn Bearer token từ phiên Auth.js', () => {
  it('có phiên → gắn Authorization: Bearer <accessToken>', async () => {
    mockGetSession.mockResolvedValue({ accessToken: 'tok-1', expires: '' });

    const result = await attachAccessToken(config());

    expect(result.headers.Authorization).toBe('Bearer tok-1');
  });

  it('chưa đăng nhập → không gắn header', async () => {
    mockGetSession.mockResolvedValue(null);

    const result = await attachAccessToken(config());

    expect(result.headers.Authorization).toBeUndefined();
  });

  it('FR-AUTH-004: phiên lỗi RefreshTokenError → không gửi token cũ', async () => {
    mockGetSession.mockResolvedValue({ accessToken: 'tok-cu', error: 'RefreshTokenError', expires: '' });

    const result = await attachAccessToken(config());

    expect(result.headers.Authorization).toBeUndefined();
  });

  it('request đã tự đặt Authorization → giữ nguyên, không gọi getSession', async () => {
    const result = await attachAccessToken(config({ Authorization: 'Bearer rieng' }));

    expect(result.headers.Authorization).toBe('Bearer rieng');
    expect(mockGetSession).not.toHaveBeenCalled();
  });

  it('nhiều request song song → chỉ gọi getSession một lần (cache 30 giây)', async () => {
    mockGetSession.mockResolvedValue({ accessToken: 'tok-2', expires: '' });

    const tokens = await Promise.all([getBrowserAccessToken(), getBrowserAccessToken(), getBrowserAccessToken()]);
    await getBrowserAccessToken();

    expect(tokens).toEqual(['tok-2', 'tok-2', 'tok-2']);
    expect(mockGetSession).toHaveBeenCalledTimes(1);
  });

  it('getSession lỗi mạng → không chặn request, chỉ không gắn token', async () => {
    mockGetSession.mockRejectedValue(new Error('network'));

    const result = await attachAccessToken(config());

    expect(result.headers.Authorization).toBeUndefined();
  });
});
