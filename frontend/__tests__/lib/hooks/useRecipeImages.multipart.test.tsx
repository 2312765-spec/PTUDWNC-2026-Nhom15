import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook } from '@testing-library/react';
import type { AxiosAdapter, InternalAxiosRequestConfig } from 'axios';
import type { ReactNode } from 'react';
import { apiClient } from '@/lib/api-client';
import { useRecipeImages } from '@/lib/hooks/useRecipeImages';

/**
 * FR-RCP-008 / CONS-007 — dùng apiClient THẬT (chỉ thay adapter) để kiểm body gửi đi vẫn là FormData.
 * apiClient đặt mặc định `Content-Type: application/json`; với header đó, transformRequest của axios
 * biến FormData thành chuỗi JSON → server trả 415 và không upload được ảnh nào. Test ở
 * useRecipeImages.test.tsx mock apiClient.post nên không bắt được lỗi này.
 */
jest.mock('next-auth/react', () => ({ getSession: jest.fn().mockResolvedValue(null) }));
jest.mock('@/components/ui/Toast', () => ({ useToast: () => ({ show: jest.fn() }) }));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

it('FR-RCP-008: upload gửi body multipart (FormData), không bị axios chuyển thành JSON', async () => {
  let sent: InternalAxiosRequestConfig | undefined;
  const adapter: AxiosAdapter = async (config) => {
    sent = config;
    return {
      data: { imageId: 'img-1', originalUrl: 'http://s3/img-1.png', altText: null, isPrimary: true },
      status: 201,
      statusText: 'Created',
      headers: {},
      config,
    };
  };
  const originalAdapter = apiClient.defaults.adapter;
  apiClient.defaults.adapter = adapter;

  try {
    const { result } = renderHook(() => useRecipeImages('recipe-1'), { wrapper });
    await result.current.uploadImage(new File(['x'], 'a.png', { type: 'image/png' }), 'Mô tả');
  } finally {
    apiClient.defaults.adapter = originalAdapter;
  }

  expect(sent?.data).toBeInstanceOf(FormData);
  expect((sent?.data as FormData).get('altText')).toBe('Mô tả');
  expect(sent?.headers.getContentType()).not.toMatch(/application\/json/);
});
