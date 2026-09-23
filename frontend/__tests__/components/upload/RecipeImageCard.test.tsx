import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RecipeImageCard } from '@/components/upload/RecipeImageCard';
import type { RecipeImageDto } from '@/lib/types';

function makeImage(overrides: Partial<RecipeImageDto> = {}): RecipeImageDto {
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

describe('RecipeImageCard', () => {
  it('ảnh primary → hiện badge "Ảnh chính", KHÔNG hiện nút đặt làm ảnh chính', () => {
    render(
      <RecipeImageCard
        image={makeImage({ isPrimary: true })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
      />,
    );

    expect(screen.getByText('Ảnh chính')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /đặt làm ảnh chính/i })).not.toBeInTheDocument();
  });

  it('ảnh không phải primary → hiện nút đặt làm ảnh chính, bấm gọi đúng onSetPrimary(imageId)', async () => {
    const onSetPrimary = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage({ imageId: 'img-2', isPrimary: false })}
        onSetPrimary={onSetPrimary}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /đặt làm ảnh chính/i }));
    expect(onSetPrimary).toHaveBeenCalledWith('img-2');
  });

  it('fallback ảnh: chưa có mediumUrl/thumbnailUrl → dùng originalUrl', () => {
    render(
      <RecipeImageCard
        image={makeImage({ originalUrl: 'http://minio/original.jpg', mediumUrl: null, thumbnailUrl: null })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
      />,
    );

    expect(screen.getByRole('img')).toHaveAttribute('src', 'http://minio/original.jpg');
  });

  it('có thumbnailUrl (resize job đã chạy xong) → ưu tiên dùng thumbnailUrl', () => {
    render(
      <RecipeImageCard
        image={makeImage({
          originalUrl: 'http://minio/original.jpg',
          thumbnailUrl: 'http://minio/thumb.jpg',
        })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
      />,
    );

    expect(screen.getByRole('img')).toHaveAttribute('src', 'http://minio/thumb.jpg');
  });

  it('bấm nút sửa mô tả → hiện input alt-text, Enter gọi onUpdateAltText đúng giá trị mới', async () => {
    const onUpdateAltText = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage({ imageId: 'img-3', altText: 'Mô tả cũ' })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={onUpdateAltText}
        onDelete={jest.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /sửa mô tả/i }));
    const input = screen.getByRole('textbox', { name: /mô tả ảnh/i });
    expect(input).toHaveValue('Mô tả cũ');

    await userEvent.clear(input);
    await userEvent.type(input, 'Mô tả mới{Enter}');

    expect(onUpdateAltText).toHaveBeenCalledWith('img-3', 'Mô tả mới');
    expect(screen.queryByRole('textbox', { name: /mô tả ảnh/i })).not.toBeInTheDocument();
  });

  it('sửa mô tả rồi bấm Escape → huỷ, KHÔNG gọi onUpdateAltText', async () => {
    const onUpdateAltText = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage({ altText: 'Giữ nguyên' })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={onUpdateAltText}
        onDelete={jest.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /sửa mô tả/i }));
    const input = screen.getByRole('textbox', { name: /mô tả ảnh/i });
    await userEvent.type(input, ' thêm chữ{Escape}');

    expect(onUpdateAltText).not.toHaveBeenCalled();
    expect(screen.queryByRole('textbox', { name: /mô tả ảnh/i })).not.toBeInTheDocument();
    expect(screen.getByText('Giữ nguyên')).toBeInTheDocument();
  });

  it('bấm nút xoá → gọi onDelete đúng imageId', async () => {
    const onDelete = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage({ imageId: 'img-4' })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={onDelete}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /xoá ảnh/i }));
    expect(onDelete).toHaveBeenCalledWith('img-4');
  });

  it('có onDragStart → card kéo được (draggable), dragStart gọi onDragStart', () => {
    const onDragStart = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onDragStart={onDragStart}
      />,
    );

    const card = screen.getByTestId('recipe-image-card');
    expect(card).toHaveAttribute('draggable', 'true');

    card.dispatchEvent(new Event('dragstart', { bubbles: true }));
    expect(onDragStart).toHaveBeenCalledTimes(1);
  });

  it('không truyền onDragStart → card không kéo được', () => {
    render(
      <RecipeImageCard image={makeImage()} onSetPrimary={jest.fn()} onUpdateAltText={jest.fn()} onDelete={jest.fn()} />,
    );

    expect(screen.getByTestId('recipe-image-card')).toHaveAttribute('draggable', 'false');
  });

  it('đang sửa mô tả → tắt draggable dù có onDragStart (tránh xung đột chọn text trong input)', async () => {
    render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onDragStart={jest.fn()}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /sửa mô tả/i }));
    expect(screen.getByTestId('recipe-image-card')).toHaveAttribute('draggable', 'false');
  });

  it('thả (drop) lên card → gọi onDrop', () => {
    const onDrop = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onDrop={onDrop}
      />,
    );

    screen.getByTestId('recipe-image-card').dispatchEvent(new Event('drop', { bubbles: true, cancelable: true }));
    expect(onDrop).toHaveBeenCalledTimes(1);
  });
});
