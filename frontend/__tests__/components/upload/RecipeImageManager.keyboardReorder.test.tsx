import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeImageManager } from '@/components/upload/RecipeImageManager';
import type { RecipeImageDto } from '@/lib/types';

/**
 * Sắp xếp ảnh bằng bàn phím, dùng useRecipeImageGallery THẬT (chỉ mock lớp gọi API) để kiểm tra
 * cả chuỗi: bấm nút → đổi thứ tự DOM → PATCH orderIndex → focus không bị mất → thông báo aria-live.
 */
const mockUpdateImage = jest.fn();

jest.mock('@/lib/hooks/useRecipeImages', () => ({
  useRecipeImages: () => ({
    uploadImage: jest.fn(),
    updateImage: mockUpdateImage,
    deleteImage: jest.fn(),
    isUploading: false,
    isUpdating: false,
    isDeleting: false,
  }),
}));

function image(imageId: string, orderIndex: number, altText: string): RecipeImageDto {
  return {
    imageId,
    originalUrl: `http://minio/${imageId}.jpg`,
    mediumUrl: null,
    thumbnailUrl: null,
    altText,
    isPrimary: orderIndex === 0,
    orderIndex,
  };
}

const initial = [image('a', 0, 'Ảnh A'), image('b', 1, 'Ảnh B'), image('c', 2, 'Ảnh C')];

function altOrder(): string[] {
  return screen.getAllByRole('img').map((img) => img.getAttribute('alt') ?? '');
}

beforeEach(() => {
  jest.clearAllMocks();
  mockUpdateImage.mockResolvedValue(undefined);
});

describe('RecipeImageManager — sắp xếp bằng nút/bàn phím (gallery thật)', () => {
  it('bấm "ra sau" ở ảnh đầu → DOM đổi thứ tự, PATCH orderIndex, focus vẫn ở nút của ảnh vừa di chuyển', async () => {
    render(<RecipeImageManager recipeId="r-1" initialImages={initial} />);
    expect(altOrder()).toEqual(['Ảnh A', 'Ảnh B', 'Ảnh C']);

    const moveLaterOfA = screen.getAllByRole('button', { name: /đưa ảnh ra sau/i })[0];
    moveLaterOfA.focus();
    await userEvent.keyboard('{Enter}');

    expect(altOrder()).toEqual(['Ảnh B', 'Ảnh A', 'Ảnh C']);
    await waitFor(() => expect(mockUpdateImage).toHaveBeenCalledWith('a', { orderIndex: 1 }));
    expect(mockUpdateImage).toHaveBeenCalledWith('b', { orderIndex: 0 });

    // Ảnh A giờ ở giữa (có cả 2 nút): focus phải đang ở chính nút "ra sau" của nó, để bấm tiếp được ngay.
    const cardOfA = screen.getByAltText('Ảnh A').closest('[data-testid="recipe-image-card"]') as HTMLElement;
    expect(cardOfA.contains(document.activeElement)).toBe(true);
    expect(document.activeElement).toHaveAccessibleName(/đưa ảnh ra sau/i);
  });

  it('bấm liên tiếp "ra sau" bằng bàn phím tới cuối → ảnh xuống cuối, focus chuyển sang nút "lên trước" (không mất focus)', async () => {
    render(<RecipeImageManager recipeId="r-1" initialImages={initial} />);

    screen.getAllByRole('button', { name: /đưa ảnh ra sau/i })[0].focus();
    await userEvent.keyboard('{Enter}'); // A: vị trí 2
    await userEvent.keyboard('{Enter}'); // A: vị trí 3 (cuối) — nút "ra sau" biến mất

    expect(altOrder()).toEqual(['Ảnh B', 'Ảnh C', 'Ảnh A']);
    expect(document.activeElement).toHaveAccessibleName(/đưa ảnh lên trước/i);
    expect(screen.getByRole('status')).toHaveTextContent(/Ảnh A.*vị trí 3\/3/);
  });
});
