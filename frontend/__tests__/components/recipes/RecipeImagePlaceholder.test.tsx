import { render, screen } from '@testing-library/react';
import { placeholderTone, RecipeImagePlaceholder } from '@/components/recipes/RecipeImagePlaceholder';

describe('RecipeImagePlaceholder (D97 — công thức chưa có ảnh)', () => {
  it('NFR-USE-002: trình đọc màn hình vẫn đọc "Chưa có hình ảnh"; phần minh họa ẩn khỏi cây truy cập', () => {
    const { container } = render(<RecipeImagePlaceholder title="Chè ba màu" slug="che-ba-mau" />);

    expect(screen.getByText('Chưa có hình ảnh')).toHaveClass('sr-only');
    expect(container.querySelector('[aria-hidden="true"]')).toHaveTextContent('C');
  });

  it('chữ cái đầu tên món, viết hoa — kể cả chữ có dấu ("đậu" → "Đ")', () => {
    const { container } = render(<RecipeImagePlaceholder title="  đậu hũ sốt cà" slug="dau-hu-sot-ca" />);

    expect(container.querySelector('[aria-hidden="true"]')).toHaveTextContent('Đ');
  });

  it('cùng slug → luôn cùng tông màu (không nhấp nháy giữa các lần render / trang)', () => {
    expect(placeholderTone('che-ba-mau')).toEqual(placeholderTone('che-ba-mau'));
  });

  it('các slug khác nhau rải ra nhiều tông màu (không dồn hết vào một màu)', () => {
    const slugs = ['che-ba-mau', 'goi-ngo-sen-tom-thit', 'tom-rim-man-ngot', 'banh-khot', 'bun-bo-hue', 'pho-bo', 'ca-kho-to'];
    const tones = new Set(slugs.map((s) => placeholderTone(s).background));

    expect(tones.size).toBeGreaterThanOrEqual(3);
  });

  it('tên rỗng → không vỡ, vẫn có nhãn cho trình đọc màn hình', () => {
    render(<RecipeImagePlaceholder title="   " slug="x" />);

    expect(screen.getByText('Chưa có hình ảnh')).toBeInTheDocument();
  });
});
