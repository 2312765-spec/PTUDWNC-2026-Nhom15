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

  // ---- Sắp xếp bằng nút (WCAG 2.1.1: mọi chức năng phải dùng được bằng bàn phím, không chỉ kéo-thả chuột) ----

  it('có onMoveEarlier/onMoveLater → hiện 2 nút "Đưa ảnh lên trước"/"ra sau", bấm gọi đúng handler', async () => {
    const onMoveEarlier = jest.fn();
    const onMoveLater = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onMoveEarlier={onMoveEarlier}
        onMoveLater={onMoveLater}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /đưa ảnh lên trước/i }));
    await userEvent.click(screen.getByRole('button', { name: /đưa ảnh ra sau/i }));

    expect(onMoveEarlier).toHaveBeenCalledTimes(1);
    expect(onMoveLater).toHaveBeenCalledTimes(1);
  });

  it('không truyền onMoveEarlier (ảnh đầu) → không có nút "lên trước"; không truyền onMoveLater (ảnh cuối) → không có nút "ra sau"', () => {
    const { rerender } = render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onMoveLater={jest.fn()}
      />,
    );
    expect(screen.queryByRole('button', { name: /đưa ảnh lên trước/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /đưa ảnh ra sau/i })).toBeInTheDocument();

    rerender(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onMoveEarlier={jest.fn()}
      />,
    );
    expect(screen.getByRole('button', { name: /đưa ảnh lên trước/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /đưa ảnh ra sau/i })).not.toBeInTheDocument();
  });

  it('bàn phím: Tab tới nút rồi Enter/Space kích hoạt được (là <button> thật)', async () => {
    const onMoveLater = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage({ isPrimary: true })}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onMoveLater={onMoveLater}
      />,
    );

    const moveLater = screen.getByRole('button', { name: /đưa ảnh ra sau/i });
    moveLater.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.keyboard(' ');

    expect(onMoveLater).toHaveBeenCalledTimes(2);
  });

  it('focusAction="later" → focus quay lại nút "ra sau" sau khi card render lại ở vị trí mới, rồi gọi onFocusHandled', () => {
    const onFocusHandled = jest.fn();
    render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onMoveEarlier={jest.fn()}
        onMoveLater={jest.fn()}
        focusAction="later"
        onFocusHandled={onFocusHandled}
      />,
    );

    expect(screen.getByRole('button', { name: /đưa ảnh ra sau/i })).toHaveFocus();
    expect(onFocusHandled).toHaveBeenCalledTimes(1);
  });

  it('focusAction="later" nhưng ảnh đã ở cuối (không còn nút "ra sau") → focus chuyển sang nút "lên trước" thay vì mất focus', () => {
    render(
      <RecipeImageCard
        image={makeImage()}
        onSetPrimary={jest.fn()}
        onUpdateAltText={jest.fn()}
        onDelete={jest.fn()}
        onMoveEarlier={jest.fn()}
        focusAction="later"
        onFocusHandled={jest.fn()}
      />,
    );

    expect(screen.getByRole('button', { name: /đưa ảnh lên trước/i })).toHaveFocus();
  });
});
