import { render } from '@testing-library/react';
import { RecipeMenuRow } from '@/components/recipes/RecipeMenuRow';
import type { RecipeSummaryDto } from '@/lib/types';

/** next/image cần cấu hình remotePatterns của Next runtime — trong jsdom thay bằng <img> thường. */
jest.mock('next/image', () => ({
  __esModule: true,
  // eslint-disable-next-line @next/next/no-img-element
  default: (props: { src: string; alt: string }) => <img src={props.src} alt={props.alt} />,
}));

const ORIGINAL = 'http://localhost:9000/culinary-blog/recipes/r1/goc.webp';
const CARD = 'http://localhost:9000/culinary-blog/recipes/r1/card.webp';

const recipe = (overrides: Partial<RecipeSummaryDto> = {}): RecipeSummaryDto => ({
  id: 'r1',
  title: 'Bánh khọt',
  slug: 'banh-khot',
  description: null,
  featuredImageUrl: ORIGINAL,
  featuredImageCardUrl: CARD,
  status: 'Published',
  prepTimeMinutes: 30,
  cookTimeMinutes: 30,
  difficulty: 'Medium',
  authorId: 'u1',
  authorName: null,
  createdAt: '2026-10-01T00:00:00Z',
  ...overrides,
});

function renderRow(r: RecipeSummaryDto) {
  return render(
    <ol>
      <RecipeMenuRow recipe={r} index={0} />
    </ol>,
  );
}

describe('RecipeMenuRow — ảnh nhỏ (D97)', () => {
  it('D97: ảnh vuông lấy từ ẢNH GỐC — biến thể card 760×320 đã bị cắt, cắt vuông lần nữa chỉ còn ~24% ảnh', () => {
    const { container } = renderRow(recipe());

    expect(container.querySelector('img')).toHaveAttribute('src', ORIGINAL);
  });

  it('không có ảnh gốc → dùng biến thể card', () => {
    const { container } = renderRow(recipe({ featuredImageUrl: null }));

    expect(container.querySelector('img')).toHaveAttribute('src', CARD);
  });

  it('D97: không có ảnh nào → khung minh họa (chữ cái đầu), không render <img>, không để ô trống', () => {
    const { container, getByText } = renderRow(recipe({ featuredImageUrl: null, featuredImageCardUrl: null }));

    expect(container.querySelector('img')).toBeNull();
    expect(getByText('Chưa có hình ảnh')).toBeInTheDocument();
    expect(container.querySelector('[aria-hidden="true"] .font-extrabold')).toHaveTextContent('B');
  });
});
