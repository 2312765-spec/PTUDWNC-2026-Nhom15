import { render } from '@testing-library/react';
import { notFound } from 'next/navigation';
import RecipeDetailPage, { generateMetadata } from '@/app/(public)/recipes/[slug]/page';
import { ApiError } from '@/lib/categories';
import { getRecipeBySlug } from '@/lib/recipes';
import { buildRecipeJsonLd } from '@/lib/seo/recipeJsonLd';
import { buildRecipeMetadata } from '@/lib/seo/recipeMeta';
import { recipeDetail } from '../../../utils/recipe';

jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getRecipeBySlug: jest.fn(),
}));
/** next/image cần cấu hình remotePatterns của Next runtime — trong jsdom thay bằng <img> thường. */
jest.mock('next/image', () => ({
  __esModule: true,
  // eslint-disable-next-line @next/next/no-img-element
  default: (props: { src: string; alt: string }) => <img src={props.src} alt={props.alt} />,
}));
jest.mock('next/navigation', () => ({
  notFound: jest.fn(() => {
    throw new Error('NEXT_NOT_FOUND');
  }),
}));

const mockGetRecipeBySlug = getRecipeBySlug as jest.Mock;
const props = { params: Promise.resolve({ slug: 'pho-bo' }) };

beforeEach(() => {
  jest.clearAllMocks();
});

describe('/recipes/[slug] — xử lý lỗi (FR-RCP-002)', () => {
  it('FR-RCP-002: lấy dữ liệu qua getRecipeBySlug(slug)', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(RecipeDetailPage(props)).rejects.toThrow('NEXT_NOT_FOUND');

    expect(mockGetRecipeBySlug).toHaveBeenCalledWith('pho-bo');
  });

  it('FR-RCP-002: công thức không tồn tại / không được xem (null) → notFound()', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(RecipeDetailPage(props)).rejects.toThrow('NEXT_NOT_FOUND');
    expect(notFound).toHaveBeenCalled();
  });

  it('NFR-USE-003: backend lỗi → ném lỗi cho error.tsx, KHÔNG gọi notFound() (không trả 404 giả)', async () => {
    mockGetRecipeBySlug.mockRejectedValue(new ApiError(503, 'UNKNOWN_ERROR'));

    await expect(RecipeDetailPage(props)).rejects.toEqual(new ApiError(503, 'UNKNOWN_ERROR'));
    expect(notFound).not.toHaveBeenCalled();
  });

  it('NFR-USE-003: metadata khi backend lỗi cũng ném lỗi, không gắn title "Không tìm thấy"', async () => {
    mockGetRecipeBySlug.mockRejectedValue(new ApiError(500, 'INTERNAL_ERROR'));

    await expect(generateMetadata(props)).rejects.toEqual(new ApiError(500, 'INTERNAL_ERROR'));
  });

  it('FR-RCP-002 / NFR-SEO-002: metadata khi không tồn tại → title "Không tìm thấy công thức" (đuôi do template layout thêm, không lặp)', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(generateMetadata(props)).resolves.toEqual({ title: 'Không tìm thấy công thức' });
  });
});

describe('/recipes/[slug] — SEO (NFR-SEO-001/002)', () => {
  const original = process.env.NEXT_PUBLIC_SITE_URL;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = 'https://culinaryblog.vn';
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = original;
  });

  it('NFR-SEO-002: generateMetadata dùng buildRecipeMetadata (title không lặp đuôi, canonical, OG, Twitter, robots)', async () => {
    const recipe = recipeDetail();
    mockGetRecipeBySlug.mockResolvedValue(recipe);

    const metadata = await generateMetadata(props);

    expect(metadata).toEqual(buildRecipeMetadata(recipe));
    expect(String(metadata.title)).not.toContain('Culinary Blog');
  });

  it('NFR-SEO-001: trang nhúng đúng một JSON-LD Recipe', async () => {
    const recipe = recipeDetail();
    mockGetRecipeBySlug.mockResolvedValue(recipe);

    const { container } = render(await RecipeDetailPage(props));

    const scripts = container.querySelectorAll('script[type="application/ld+json"]');
    expect(scripts).toHaveLength(1);
    expect(JSON.parse(scripts[0].innerHTML)).toEqual(buildRecipeJsonLd(recipe, 'https://culinaryblog.vn'));
  });

  it('FR-RCP-002: công thức chưa có ảnh → khung "Chưa có hình ảnh", không render <img> tới file không tồn tại', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail({ images: [] }));

    const { getByText, queryByAltText } = render(await RecipeDetailPage(props));

    expect(getByText('Chưa có hình ảnh')).toBeInTheDocument();
    // Ảnh của từng bước (nếu có) vẫn hiện; chỉ ảnh đại diện món là không có.
    expect(queryByAltText('Phở bò Hà Nội')).toBeNull();
  });

  it('NFR-SEO-001: mỗi bước có id="buoc-n" khớp HowToStep.url', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());

    const { container } = render(await RecipeDetailPage(props));

    expect(container.querySelector('#buoc-1')).toHaveTextContent('Sơ chế xương');
    expect(container.querySelector('#buoc-2')).toHaveTextContent('Ninh nước dùng');
  });
});

describe('/recipes/[slug] — giao diện sổ tay (D97)', () => {
  const timedSteps = [
    { id: 's2', stepNumber: 2, title: 'Ninh nước dùng', description: 'Ninh xương.', timerMinutes: 480 },
    { id: 's1', stepNumber: 1, title: 'Sơ chế xương', description: 'Chần xương.', timerMinutes: 30 },
    { id: 's3', stepNumber: 3, title: 'Trụng bánh', description: 'Trụng bánh phở.', timerMinutes: 5 },
  ];

  it('D97: mọi bước có timerMinutes → dòng thời gian cộng dồn (phút 0, phút 30, 8 giờ 30 phút) + mốc dọn ra đĩa', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail({ steps: timedSteps }));

    const { container, getByText } = render(await RecipeDetailPage(props));

    expect(container.querySelector('#buoc-1')).toHaveTextContent('phút 0');
    expect(container.querySelector('#buoc-2')).toHaveTextContent('phút 30');
    expect(container.querySelector('#buoc-3')).toHaveTextContent('8 giờ 30 phút');
    expect(getByText('Dọn ra đĩa')).toBeInTheDocument();
    expect(getByText('8 giờ 35 phút')).toBeInTheDocument();
  });

  it('D97: có bước thiếu timerMinutes → mốc "Bước n" (không cộng dồn sai), không có mốc dọn ra đĩa', async () => {
    mockGetRecipeBySlug.mockResolvedValue(
      recipeDetail({ steps: [timedSteps[1], { ...timedSteps[0], timerMinutes: undefined }] }),
    );

    const { container, queryByText } = render(await RecipeDetailPage(props));

    expect(container.querySelector('#buoc-1')).toHaveTextContent('Bước 1');
    expect(container.querySelector('#buoc-1')).not.toHaveTextContent('phút 0');
    expect(container.querySelector('#buoc-2')).toHaveTextContent('Bước 2');
    expect(queryByText('Dọn ra đĩa')).toBeNull();
  });

  it('D97: bước hiện theo stepNumber, nguyên liệu theo orderIndex dù API trả lộn thứ tự', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());

    const { getAllByRole } = render(await RecipeDetailPage(props));

    const boxes = getAllByRole('checkbox');
    expect(boxes.map((b) => b.closest('label')?.textContent)).toEqual([
      expect.stringContaining('xương bò'),
      expect.stringContaining('bánh phở'),
      expect.stringContaining('hành lá'),
    ]);
    const stepHeadings = getAllByRole('heading', { level: 3 }).map((h) => h.textContent);
    expect(stepHeadings[0]).toContain('Sơ chế xương');
    expect(stepHeadings[1]).toContain('Ninh nước dùng');
  });

  it('D97: nguyên liệu là danh sách đi chợ có ô tick, nhãn gồm tên + lượng; heading giữ "Nguyên liệu"', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());

    const { getByRole } = render(await RecipeDetailPage(props));

    expect(getByRole('heading', { level: 2, name: 'Nguyên liệu' })).toBeInTheDocument();
    expect(getByRole('heading', { level: 2, name: 'Các bước thực hiện' })).toBeInTheDocument();
    expect(getByRole('checkbox', { name: /xương bò.*1\.5 kg/ })).not.toBeChecked();
  });

  it('D97: dòng thông số — chuẩn bị, nấu (theo giờ khi ≥ 60 phút), khẩu phần, độ khó, tác giả', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());

    const { getByText } = render(await RecipeDetailPage(props));

    expect(getByText('30 phút')).toBeInTheDocument();
    expect(getByText('8 giờ')).toBeInTheDocument();
    expect(getByText('4 người')).toBeInTheDocument();
    expect(getByText('Trung bình')).toBeInTheDocument();
    expect(getByText('Nguyễn Văn An')).toBeInTheDocument();
  });
});
