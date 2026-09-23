import { act, renderHook } from '@testing-library/react';
import type { RecipeImageDto } from '@/lib/types';
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
    let resolveUpload!: (image: RecipeImageDto) => void;
    let capturedOnProgress: ((percent: number) => void) | undefined;

    mockUploadImage.mockImplementationOnce(
      (_file: File, _altText: string | undefined, onProgress?: (percent: number) => void) => {
        capturedOnProgress = onProgress;
        return new Promise<RecipeImageDto>((resolve) => {
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

    const uploaded = image({ imageId: 'img-1', isPrimary: true });
    await act(async () => {
      resolveUpload(uploaded);
      await uploadPromise;
    });

    expect(result.current.uploadingFiles).toHaveLength(0);
    expect(result.current.images).toEqual([uploaded]);
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
