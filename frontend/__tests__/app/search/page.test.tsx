import { screen } from '@testing-library/react';
import SearchPage, { dynamic, generateMetadata } from '@/app/(public)/search/page';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/app/(public)/search/SearchResults', () => ({
  __esModule: true,
  default: ({ q, page }: { q: string; page: number }) => (
    <div data-testid="results">
      {q}|{page}
    </div>
  ),
}));

const props = (value: Record<string, string | string[]> = {}) => ({
  searchParams: Promise.resolve(value),
});

describe('/search — FR-SRCH-001', () => {
  it('FR-SRCH-001: SRS 5.1 — trang render SSR (force-dynamic)', () => {
    expect(dynamic).toBe('force-dynamic');
  });

  it('FR-SRCH-001: chưa có q → chỉ có h1 + form, chưa gọi kết quả', async () => {
    renderWithProviders(await SearchPage(props()));

    expect(screen.getByRole('heading', { level: 1, name: 'Tìm kiếm công thức' })).toBeInTheDocument();
    expect(screen.getByRole('search')).toBeInTheDocument();
    expect(screen.queryByTestId('results')).not.toBeInTheDocument();
  });

  it('FR-SRCH-001: q < 2 ký tự → không gọi kết quả', async () => {
    renderWithProviders(await SearchPage(props({ q: ' a ' })));

    expect(screen.queryByTestId('results')).not.toBeInTheDocument();
  });

  it('FR-SRCH-001/004: đọc q (trim) và page từ URL, page lạ → 1', async () => {
    renderWithProviders(await SearchPage(props({ q: '  pho bo ', page: '3' })));
    expect(screen.getByTestId('results')).toHaveTextContent('pho bo|3');
    expect(screen.getByRole('searchbox')).toHaveValue('pho bo');
  });

  it('FR-SRCH-004: page không hợp lệ → về trang 1', async () => {
    renderWithProviders(await SearchPage(props({ q: 'pho', page: '-2' })));

    expect(screen.getByTestId('results')).toHaveTextContent('pho|1');
  });

  it('FR-SRCH-001: metadata title theo từ khóa', async () => {
    await expect(generateMetadata(props({ q: 'pho' }))).resolves.toEqual({ title: 'Tìm kiếm: pho' });
    await expect(generateMetadata(props())).resolves.toEqual({ title: 'Tìm kiếm công thức' });
  });
});
