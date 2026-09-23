import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { AxiosError } from 'axios';
import type { ReactNode } from 'react';
import { apiClient } from '@/lib/api-client';
import { useRecipeImages } from '@/lib/hooks/useRecipeImages';

/**
 * FR-RCP-008 (FE) — hợp đồng backend đã chốt ở D22/D27:
 * POST /recipes/{id}/images (multipart: file, altText?) → 201 { imageId, originalUrl, altText, isPrimary }
 * PATCH /recipes/{id}/images/{imageId} { altText?, isPrimary?, orderIndex? } → 200
 * DELETE /recipes/{id}/images/{imageId} → 204
 */
jest.mock('@/lib/api-client', () => {
  const actual = jest.requireActual('@/lib/api-client');
  return {
    ...actual,
    apiClient: { post: jest.fn(), patch: jest.fn(), delete: jest.fn() },
  };
});

const mockShow = jest.fn();
jest.mock('@/components/ui/Toast', () => ({
  useToast: () => ({ show: mockShow }),
}));

const mockedApiClient = apiClient as unknown as {
  post: jest.Mock;
  patch: jest.Mock;
  delete: jest.Mock;
};

const RECIPE_ID = '11111111-1111-1111-1111-111111111111';
const IMAGE_ID = '22222222-2222-2222-2222-222222222222';

function problemError(status: number, type: string, detail = 'Lỗi') {
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', undefined, undefined, {
    status,
    statusText: 'Bad Request',
    headers: {},
    config: {} as never,
    data: { type, title: type, status, detail },
  });
}

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  jest.clearAllMocks();
});

describe('useRecipeImages — upload', () => {
  it('upload thành công: gửi multipart đúng field, trả về RecipeImageDto, toast success', async () => {
    mockedApiClient.post.mockResolvedValueOnce({
      data: { imageId: IMAGE_ID, originalUrl: 'http://minio/x.jpg', altText: 'Phở bò', isPrimary: true },
    });

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });
    const file = new File(['x'], 'pho.jpg', { type: 'image/jpeg' });

    const image = await result.current.uploadImage(file, 'Phở bò');

    expect(image).toEqual({ imageId: IMAGE_ID, originalUrl: 'http://minio/x.jpg', altText: 'Phở bò', isPrimary: true });

    expect(mockedApiClient.post).toHaveBeenCalledTimes(1);
    const [url, body] = mockedApiClient.post.mock.calls[0];
    expect(url).toBe(`/recipes/${RECIPE_ID}/images`);
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get('file')).toBe(file);
    expect((body as FormData).get('altText')).toBe('Phở bò');

    expect(mockShow).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });

  it('CONS-007/A2: FILE_SIZE_EXCEEDED → toast lỗi đúng message tiếng Việt, promise reject', async () => {
    mockedApiClient.post.mockRejectedValueOnce(problemError(400, 'FILE_SIZE_EXCEEDED'));

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });
    const file = new File(['x'], 'big.jpg', { type: 'image/jpeg' });

    await expect(result.current.uploadImage(file)).rejects.toBeTruthy();

    await waitFor(() =>
      expect(mockShow).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'error',
          description: 'Kích thước file vượt quá giới hạn 5MB.',
        }),
      ),
    );
  });

  it('D28/A3: FILE_MIME_INVALID → toast lỗi đúng message tiếng Việt', async () => {
    mockedApiClient.post.mockRejectedValueOnce(problemError(400, 'FILE_MIME_INVALID'));

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });
    const file = new File(['x'], 'fake.jpg', { type: 'image/jpeg' });

    await expect(result.current.uploadImage(file)).rejects.toBeTruthy();

    await waitFor(() =>
      expect(mockShow).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'error',
          description: 'File không đúng định dạng. Chỉ chấp nhận JPG, PNG, WebP, AVIF.',
        }),
      ),
    );
  });

  it('báo tiến trình upload qua onProgress khi axios bắn onUploadProgress', async () => {
    mockedApiClient.post.mockImplementationOnce((_url, _body, config) => {
      config.onUploadProgress?.({ loaded: 50, total: 100 } as never);
      return Promise.resolve({
        data: { imageId: IMAGE_ID, originalUrl: 'http://minio/x.jpg', altText: null, isPrimary: true },
      });
    });

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });
    const onProgress = jest.fn();
    const file = new File(['x'], 'pho.jpg', { type: 'image/jpeg' });

    await result.current.uploadImage(file, undefined, onProgress);

    expect(onProgress).toHaveBeenCalledWith(50);
  });
});

describe('useRecipeImages — update', () => {
  it('D22: đặt ảnh chính → PATCH đúng body, toast success', async () => {
    mockedApiClient.patch.mockResolvedValueOnce({ status: 200 });

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });
    await result.current.updateImage(IMAGE_ID, { isPrimary: true });

    expect(mockedApiClient.patch).toHaveBeenCalledWith(
      `/recipes/${RECIPE_ID}/images/${IMAGE_ID}`,
      { isPrimary: true },
    );
    expect(mockShow).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });

  it('D22/D27: RECIPE_PRIMARY_IMAGE_REQUIRED → toast đúng message', async () => {
    mockedApiClient.patch.mockRejectedValueOnce(problemError(400, 'RECIPE_PRIMARY_IMAGE_REQUIRED'));

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });

    await expect(result.current.updateImage(IMAGE_ID, { isPrimary: false })).rejects.toBeTruthy();

    await waitFor(() =>
      expect(mockShow).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'error',
          description: 'Công thức phải luôn có 1 ảnh chính.',
        }),
      ),
    );
  });

  it('FR-RCP-008: RECIPE_IMAGE_NOT_FOUND → toast đúng message', async () => {
    mockedApiClient.patch.mockRejectedValueOnce(problemError(404, 'RECIPE_IMAGE_NOT_FOUND'));

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });

    await expect(result.current.updateImage(IMAGE_ID, { altText: 'x' })).rejects.toBeTruthy();

    await waitFor(() =>
      expect(mockShow).toHaveBeenCalledWith(
        expect.objectContaining({ variant: 'error', description: 'Không tìm thấy ảnh.' }),
      ),
    );
  });
});

describe('useRecipeImages — delete', () => {
  it('xoá ảnh thành công → gọi DELETE đúng URL, toast success', async () => {
    mockedApiClient.delete.mockResolvedValueOnce({ status: 204 });

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });
    await result.current.deleteImage(IMAGE_ID);

    expect(mockedApiClient.delete).toHaveBeenCalledWith(`/recipes/${RECIPE_ID}/images/${IMAGE_ID}`);
    expect(mockShow).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });

  it('permissions.md: RECIPE_FORBIDDEN → toast đúng message', async () => {
    mockedApiClient.delete.mockRejectedValueOnce(problemError(403, 'RECIPE_FORBIDDEN'));

    const { result } = renderHook(() => useRecipeImages(RECIPE_ID), { wrapper });

    await expect(result.current.deleteImage(IMAGE_ID)).rejects.toBeTruthy();

    await waitFor(() =>
      expect(mockShow).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'error',
          description: 'Bạn không có quyền sửa ảnh của công thức này.',
        }),
      ),
    );
  });
});
