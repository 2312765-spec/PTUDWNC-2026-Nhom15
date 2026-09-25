import { act, renderHook } from '@testing-library/react';
import type { RecipeImageDto, UploadRecipeImageResponse } from '@/lib/types';
import { useRecipeImageGallery } from '@/lib/hooks/useRecipeImageGallery';

/**
 * useRecipeImageGallery bọc useRecipeImages để giữ state mảng ảnh cục bộ cho
 * RecipeImageManager — không phụ thuộc GET recipe detail (FR-RCP-002 chưa xong).
 * Test này mock thẳng useRecipeImages (đã có test riêng ở useRecipeImages.test.tsx)
 * để chỉ kiểm logic composition/state, không lặp lại việc gọi API thật.
 */
const mockUploadImage = jest.fn();
const mockUpdateImage = jest.fn();
const mockDeleteImage = jest.fn();

jest.mock('@/lib/hooks/useRecipeImages', () => ({
  useRecipeImages: () => ({
    uploadImage: mockUploadImage,
    updateImage: mockUpdateImage,
    deleteImage: mockDeleteImage,
    isUploading: false,
    isUpdating: false,
    isDeleting: false,
  }),
}));

const RECIPE_ID = '11111111-1111-1111-1111-111111111111';

/** Đúng 4 trường backend trả về ở POST /images (D27) — KHÔNG có orderIndex/mediumUrl/thumbnailUrl. */
function uploadResponse(overrides: Partial<UploadRecipeImageResponse> = {}): UploadRecipeImageResponse {
  return {
    imageId: 'img-new',
    originalUrl: 'http://minio/img-new.jpg',
    altText: null,
    isPrimary: false,
    ...overrides,
  };
}

function image(overrides: Partial<RecipeImageDto>): RecipeImageDto {
  return {
    imageId: 'img-1',
    originalUrl: 'http://minio/img-1.jpg',
    mediumUrl: null,
    thumbnailUrl: null,
    altText: null,
    isPrimary: false,
    orderIndex: 0,
    ...overrides,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
});

describe('useRecipeImageGallery — upload', () => {
  it('thêm placeholder đang tải, báo tiến trình, rồi thay bằng ảnh thật khi xong', async () => {
    let resolveUpload!: (image: UploadRecipeImageResponse) => void;
    let capturedOnProgress: ((percent: number) => void) | undefined;

    mockUploadImage.mockImplementationOnce(
      (_file: File, _altText: string | undefined, onProgress?: (percent: number) => void) => {
        capturedOnProgress = onProgress;
        return new Promise<UploadRecipeImageResponse>((resolve) => {
          resolveUpload = resolve;
        });
      },
    );

    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID));
    const file = new File(['x'], 'pho.jpg', { type: 'image/jpeg' });

    let uploadPromise!: Promise<void>;
    act(() => {
      uploadPromise = result.current.upload(file);
    });

    expect(result.current.uploadingFiles).toHaveLength(1);
    expect(result.current.uploadingFiles[0]).toMatchObject({ fileName: 'pho.jpg', progress: 0 });

    act(() => {
      capturedOnProgress?.(40);
    });
    expect(result.current.uploadingFiles[0].progress).toBe(40);

    await act(async () => {
      resolveUpload(uploadResponse({ imageId: 'img-1', isPrimary: true }));
      await uploadPromise;
    });

    expect(result.current.uploadingFiles).toHaveLength(0);
    // Backend không trả orderIndex/mediumUrl/thumbnailUrl → gallery phải điền: ảnh đầu tiên = 0, url resize = null.
    expect(result.current.images).toEqual([
      expect.objectContaining({
        imageId: 'img-1',
        isPrimary: true,
        orderIndex: 0,
        mediumUrl: null,
        thumbnailUrl: null,
      }),
    ]);
  });

  it('upload vào gallery đã có ảnh → orderIndex = lớn nhất hiện có + 1, ảnh mới xếp cuối (không NaN/undefined)', async () => {
    mockUploadImage.mockResolvedValueOnce(uploadResponse({ imageId: 'img-new' }));
    const { result } = renderHook(() =>
      useRecipeImageGallery(RECIPE_ID, [
        image({ imageId: 'a', orderIndex: 0, isPrimary: true }),
        image({ imageId: 'b', orderIndex: 4 }),
      ]),
    );

    await act(async () => {
      await result.current.upload(new File(['x'], 'moi.jpg', { type: 'image/jpeg' }));
    });

    expect(result.current.images.map((img) => [img.imageId, img.orderIndex])).toEqual([
      ['a', 0],
      ['b', 4],
      ['img-new', 5],
    ]);
  });

  it('upload liên tiếp nhiều ảnh → orderIndex tăng dần 0, 1, 2 và chỉ ảnh đầu là primary', async () => {
    mockUploadImage
      .mockResolvedValueOnce(uploadResponse({ imageId: 'i1', isPrimary: true }))
      .mockResolvedValueOnce(uploadResponse({ imageId: 'i2' }))
      .mockResolvedValueOnce(uploadResponse({ imageId: 'i3' }));
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID));

    for (const name of ['1.jpg', '2.jpg', '3.jpg']) {
      await act(async () => {
        await result.current.upload(new File(['x'], name, { type: 'image/jpeg' }));
      });
    }

    expect(result.current.images.map((img) => [img.imageId, img.orderIndex, img.isPrimary])).toEqual([
      ['i1', 0, true],
      ['i2', 1, false],
      ['i3', 2, false],
    ]);
  });

  it('upload lỗi → dọn placeholder, không kẹt trạng thái đang tải, không thêm ảnh giả', async () => {
    mockUploadImage.mockRejectedValueOnce(new Error('FILE_SIZE_EXCEEDED'));

    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID));
    const file = new File(['x'], 'big.jpg', { type: 'image/jpeg' });

    await act(async () => {
      await expect(result.current.upload(file)).rejects.toThrow();
    });

    expect(result.current.uploadingFiles).toHaveLength(0);
    expect(result.current.images).toHaveLength(0);
  });
});

describe('useRecipeImageGallery — setPrimary (D22)', () => {
  it('đặt ảnh khác làm primary → ảnh đó true, các ảnh còn lại về false trong state cục bộ', async () => {
    mockUpdateImage.mockResolvedValueOnce(undefined);

    const initial = [
      image({ imageId: 'img-1', isPrimary: true, orderIndex: 0 }),
      image({ imageId: 'img-2', isPrimary: false, orderIndex: 1 }),
    ];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.setPrimary('img-2');
    });

    expect(mockUpdateImage).toHaveBeenCalledWith('img-2', { isPrimary: true });
    expect(result.current.images.find((i) => i.imageId === 'img-1')?.isPrimary).toBe(false);
    expect(result.current.images.find((i) => i.imageId === 'img-2')?.isPrimary).toBe(true);
  });
});

describe('useRecipeImageGallery — updateAltText', () => {
  it('chỉ cập nhật alt text của đúng ảnh, không đụng ảnh khác', async () => {
    mockUpdateImage.mockResolvedValueOnce(undefined);

    const initial = [
      image({ imageId: 'img-1', altText: 'cũ 1' }),
      image({ imageId: 'img-2', altText: 'cũ 2' }),
    ];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.updateAltText('img-2', 'mới');
    });

    expect(mockUpdateImage).toHaveBeenCalledWith('img-2', { altText: 'mới' });
    expect(result.current.images.find((i) => i.imageId === 'img-1')?.altText).toBe('cũ 1');
    expect(result.current.images.find((i) => i.imageId === 'img-2')?.altText).toBe('mới');
  });
});

describe('useRecipeImageGallery — remove (D22/D1)', () => {
  it('xoá ảnh không phải primary → chỉ mất khỏi state, primary hiện tại giữ nguyên', async () => {
    mockDeleteImage.mockResolvedValueOnce(undefined);

    const initial = [
      image({ imageId: 'img-1', isPrimary: true, orderIndex: 0 }),
      image({ imageId: 'img-2', isPrimary: false, orderIndex: 1 }),
    ];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.remove('img-2');
    });

    expect(result.current.images.map((i) => i.imageId)).toEqual(['img-1']);
    expect(result.current.images[0].isPrimary).toBe(true);
  });

  it('xoá ảnh đang primary, còn ảnh khác → ảnh orderIndex nhỏ nhất tự lên primary', async () => {
    mockDeleteImage.mockResolvedValueOnce(undefined);

    const initial = [
      image({ imageId: 'img-1', isPrimary: true, orderIndex: 0 }),
      image({ imageId: 'img-2', isPrimary: false, orderIndex: 2 }),
      image({ imageId: 'img-3', isPrimary: false, orderIndex: 1 }),
    ];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.remove('img-1');
    });

    expect(result.current.images.map((i) => i.imageId)).toEqual(['img-3', 'img-2']);
    expect(result.current.images.find((i) => i.imageId === 'img-3')?.isPrimary).toBe(true);
    expect(result.current.images.find((i) => i.imageId === 'img-2')?.isPrimary).toBe(false);
  });

  it('xoá ảnh cuối cùng → danh sách rỗng', async () => {
    mockDeleteImage.mockResolvedValueOnce(undefined);

    const initial = [image({ imageId: 'img-1', isPrimary: true })];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.remove('img-1');
    });

    expect(result.current.images).toEqual([]);
  });
});

describe('useRecipeImageGallery — reorder (D27: PATCH orderIndex, cho phép trùng, không renumber)', () => {
  it('kéo ảnh đầu thả vào vị trí ảnh cuối → sắp xếp lại ngay (optimistic), orderIndex đánh lại tuần tự', async () => {
    mockUpdateImage.mockResolvedValue(undefined);

    const initial = [
      image({ imageId: 'img-1', orderIndex: 0 }),
      image({ imageId: 'img-2', orderIndex: 1 }),
      image({ imageId: 'img-3', orderIndex: 2 }),
    ];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    act(() => {
      void result.current.reorder('img-1', 'img-3');
    });

    expect(result.current.images.map((i) => i.imageId)).toEqual(['img-2', 'img-3', 'img-1']);
    expect(result.current.images.map((i) => i.orderIndex)).toEqual([0, 1, 2]);
  });

  it('chỉ gọi updateImage(orderIndex) cho ảnh có orderIndex thật sự đổi', async () => {
    mockUpdateImage.mockResolvedValue(undefined);

    const initial = [
      image({ imageId: 'img-1', orderIndex: 0 }),
      image({ imageId: 'img-2', orderIndex: 1 }),
      image({ imageId: 'img-3', orderIndex: 2 }),
    ];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.reorder('img-1', 'img-2');
    });

    // img-1: 0→1, img-2: 1→0, img-3: 2→2 (không đổi, không gọi)
    expect(mockUpdateImage).toHaveBeenCalledTimes(2);
    expect(mockUpdateImage).toHaveBeenCalledWith('img-1', { orderIndex: 1 });
    expect(mockUpdateImage).toHaveBeenCalledWith('img-2', { orderIndex: 0 });
    expect(mockUpdateImage).not.toHaveBeenCalledWith('img-3', expect.anything());
  });

  it('thả vào đúng vị trí cũ của chính nó → không đổi gì, không gọi updateImage', async () => {
    const initial = [image({ imageId: 'img-1', orderIndex: 0 }), image({ imageId: 'img-2', orderIndex: 1 })];
    const { result } = renderHook(() => useRecipeImageGallery(RECIPE_ID, initial));

    await act(async () => {
      await result.current.reorder('img-1', 'img-1');
    });

    expect(mockUpdateImage).not.toHaveBeenCalled();
    expect(result.current.images.map((i) => i.imageId)).toEqual(['img-1', 'img-2']);
  });
});
