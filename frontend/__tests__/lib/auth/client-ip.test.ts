/**
 * @jest-environment node
 */
import { headers } from 'next/headers';
import { getClientIp } from '@/lib/auth/client-ip';

jest.mock('next/headers', () => ({ headers: jest.fn() }));
const mockHeaders = headers as jest.MockedFunction<typeof headers>;

function requestHeaders(init: Record<string, string>) {
  return new Headers(init) as unknown as Awaited<ReturnType<typeof headers>>;
}

describe('getClientIp — NFR-SEC-003/D63', () => {
  it('D63: lấy X-Real-IP do Nginx ghi', async () => {
    mockHeaders.mockResolvedValue(requestHeaders({ 'x-real-ip': ' 203.0.113.7 ' }));

    await expect(getClientIp()).resolves.toBe('203.0.113.7');
  });

  it('D63: KHÔNG tin X-Forwarded-For (phần đầu chuỗi do client tự gửi)', async () => {
    mockHeaders.mockResolvedValue(requestHeaders({ 'x-forwarded-for': '1.2.3.4, 203.0.113.7' }));

    await expect(getClientIp()).resolves.toBeUndefined();
  });

  it('D63: ngoài request scope (headers() ném lỗi) → undefined, không làm hỏng lời gọi API', async () => {
    mockHeaders.mockRejectedValue(new Error('headers was called outside a request scope'));

    await expect(getClientIp()).resolves.toBeUndefined();
  });
});
