import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeImageManager } from '@/components/upload/RecipeImageManager';
import { useRecipeImageGallery } from '@/lib/hooks/useRecipeImageGallery';
import type { RecipeImageDto } from '@/lib/types';

/**
 * Mock thẳng useRecipeImageGallery (đã có test riêng ở useRecipeImageGallery.test.tsx) —
 * test này chỉ kiểm việc RecipeImageManager ghép Dropzone/Card/Dialog đúng, không lặp lại
 * logic state đã test.
 */
jest.mock('@/lib/hooks/useRecipeImageGallery');

const mockedUseGallery = useRecipeImageGallery as jest.Mock;

const RECIPE_ID = '11111111-1111-1111-1111-111111111111';

function image(overrides: Partial<RecipeImageDto> = {}): RecipeImageDto {
  return {
    imageId: 'img-1',
    originalUrl: 'http://minio/img-1.jpg',
    mediumUrl: null,
    thumbnailUrl: null,
    altText: 'Phở bò',
    isPrimary: false,
    orderIndex: 0,
    ...overrides,
  };
}

function setupGallery(overrides: Partial<ReturnType<typeof useRecipeImageGallery>> = {}) {
  const gallery = {
    images: [] as RecipeImageDto[],
    uploadingFiles: [] as { id: string; fileName: string; progress: number }[],
    upload: jest.fn().mockResolvedValue(undefined),
    setPrimary: jest.fn().mockResolvedValue(undefined),
    updateAltText: jest.fn().mockResolvedValue(undefined),
    remove: jest.fn().mockResolvedValue(undefined),
    reorder: jest.fn().mockResolvedValue(undefined),
    isUploading: false,
    isUpdating: false,
    isDeleting: false,
    ...overrides,
  };
  mockedUseGallery.mockReturnValue(gallery);
  return gallery;
}

beforeEach(() => {
  jest.clearAllMocks();
});

describe('RecipeImageManager', () => {
  it('chưa có ảnh nào → hiện dropzone, không hiện lưới ảnh', () => {
    setupGallery({ images: [] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    expect(screen.getByText('Kéo thả ảnh vào đây')).toBeInTheDocument();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('có ảnh → hiện đúng số lượng card', () => {
    setupGallery({ images: [image({ imageId: 'img-1' }), image({ imageId: 'img-2', isPrimary: true })] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    expect(screen.getAllByRole('img')).toHaveLength(2);
  });

  it('chọn file mới từ dropzone → gọi gallery.upload đúng file', async () => {
    const gallery = setupGallery();
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    const file = new File([new Uint8Array(1024)], 'pho.jpg', { type: 'image/jpeg' });
    const input = screen.getByLabelText(/chọn ảnh/i, { selector: 'input' });
    await userEvent.upload(input, file);

    expect(gallery.upload).toHaveBeenCalledWith(file);
  });

  it('uploadingFiles có item → hiện tile tiến trình với tên file + %', () => {
    setupGallery({ uploadingFiles: [{ id: 'u-1', fileName: 'banhmi.jpg', progress: 42 }] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    expect(screen.getByText('banhmi.jpg')).toBeInTheDocument();
    expect(screen.getByText(/42%/)).toBeInTheDocument();
  });

  it('bấm đặt làm ảnh chính trên card → gọi gallery.setPrimary ngay, không cần xác nhận', async () => {
    const gallery = setupGallery({ images: [image({ imageId: 'img-2', isPrimary: false })] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    await userEvent.click(screen.getByRole('button', { name: /đặt làm ảnh chính/i }));
    expect(gallery.setPrimary).toHaveBeenCalledWith('img-2');
  });

  it('bấm xoá trên card → mở dialog xác nhận, CHƯA gọi gallery.remove', async () => {
    const gallery = setupGallery({ images: [image({ imageId: 'img-3' })] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    await userEvent.click(screen.getByRole('button', { name: /xoá ảnh/i }));

    expect(screen.getByText('Xoá ảnh này?')).toBeInTheDocument();
    expect(gallery.remove).not.toHaveBeenCalled();
  });

  it('trong dialog xác nhận, bấm "Xoá ảnh" → gọi gallery.remove đúng imageId rồi đóng dialog', async () => {
    const gallery = setupGallery({ images: [image({ imageId: 'img-3' })] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    await userEvent.click(screen.getByRole('button', { name: /xoá ảnh/i }));
    const dialog = screen.getByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Xoá ảnh' }));

    expect(gallery.remove).toHaveBeenCalledWith('img-3');
  });

  it('trong dialog xác nhận, bấm "Huỷ" → KHÔNG gọi gallery.remove', async () => {
    const gallery = setupGallery({ images: [image({ imageId: 'img-3' })] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    await userEvent.click(screen.getByRole('button', { name: /xoá ảnh/i }));
    const dialog = screen.getByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Huỷ' }));

    expect(gallery.remove).not.toHaveBeenCalled();
  });

  it('sửa alt text trên card → gọi gallery.updateAltText đúng tham số', async () => {
    const gallery = setupGallery({ images: [image({ imageId: 'img-4', altText: 'cũ' })] });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    await userEvent.click(screen.getByRole('button', { name: /sửa mô tả/i }));
    const input = screen.getByRole('textbox', { name: /mô tả ảnh/i });
    await userEvent.clear(input);
    await userEvent.type(input, 'mới{Enter}');

    expect(gallery.updateAltText).toHaveBeenCalledWith('img-4', 'mới');
  });

  it('truyền initialImages xuống useRecipeImageGallery đúng recipeId', () => {
    setupGallery();
    const initial = [image({ imageId: 'seed' })];
    render(<RecipeImageManager recipeId={RECIPE_ID} initialImages={initial} />);

    expect(mockedUseGallery).toHaveBeenCalledWith(RECIPE_ID, initial);
  });

  it('kéo card A thả vào card B → gọi gallery.reorder(A, B)', () => {
    const gallery = setupGallery({
      images: [image({ imageId: 'img-a' }), image({ imageId: 'img-b', orderIndex: 1 })],
    });
    render(<RecipeImageManager recipeId={RECIPE_ID} />);

    const cards = screen.getAllByTestId('recipe-image-card');
    cards[0].dispatchEvent(new Event('dragstart', { bubbles: true }));
    cards[1].dispatchEvent(new Event('drop', { bubbles: true, cancelable: true }));

    expect(gallery.reorder).toHaveBeenCalledWith('img-a', 'img-b');
  });
});
