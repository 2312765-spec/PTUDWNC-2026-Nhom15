/**
 * @jest-environment node
 */
import { unstable_doesMiddlewareMatch } from 'next/experimental/testing/server';
import { config } from '@/proxy';

// Proxy chỉ tái xuất auth() của Auth.js — test ở đây chỉ kiểm tra PHẠM VI chạy (matcher).
jest.mock('@/auth', () => ({ auth: jest.fn() }));

// Docs Next 16 (proxy.md) gọi hàm này là unstable_doesProxyMatch, nhưng bản next đang cài
// (16.3.5) chỉ export tên cũ unstable_doesMiddlewareMatch — cùng chức năng.
const matches = (url: string) => unstable_doesMiddlewareMatch({ config, nextConfig: {}, url });

describe('proxy.ts — FR-AUTH-004', () => {
  it.each(['/', '/dashboard', '/dashboard/recipes/new', '/recipes/pho-bo', '/auth/login'])(
    'FR-AUTH-004: chạy cho trang %s (refresh token + ghi cookie trước khi render)',
    (url) => {
      expect(matches(url)).toBe(true);
    },
  );

  it.each([
    '/api/auth/session',
    '/api/auth/callback/google',
    '/_next/static/chunks/main.js',
    '/_next/image',
    '/favicon.ico',
    '/images/logo.png',
  ])('FR-AUTH-004: KHÔNG chạy cho %s (endpoint Auth.js, tài nguyên tĩnh)', (url) => {
    expect(matches(url)).toBe(false);
  });
});
