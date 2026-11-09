import { screen } from '@testing-library/react';
import { isValidElement, Suspense, type ReactElement, type ReactNode } from 'react';
import SearchPage, { dynamic, generateMetadata } from '@/app/(public)/search/page';
import SearchResults from '@/app/(public)/search/SearchResults';
import { renderWithProviders } from '../../utils/render';

/** Duyệt cây JSX (chưa render) tìm phần tử thỏa điều kiện. */
function findElements(node: ReactNode, match: (el: ReactElement) => boolean): ReactElement[] {
  if (Array.isArray(node)) return node.flatMap((child) => findElements(child, match));
  if (!isValidElement(node)) return [];
  const children = (node.props as { children?: ReactNode }).children;
  return [...(match(node) ? [node] : []), ...findElements(children, match)];
}

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

  it('NFR-PERF-005/D86: kết quả KHÔNG bọc trong <Suspense> — nằm ngay trong HTML đầu, không stream trong <div hidden>', async () => {
    const tree = await SearchPage(props({ q: 'pho' }));

    expect(findElements(tree, (el) => el.type === Suspense)).toHaveLength(0);
    expect(findElements(tree, (el) => el.type === SearchResults)).toHaveLength(1);
  });

  it('FR-SRCH-001: metadata title theo từ khóa', async () => {
    // canonical + noindex (D61.4) kiểm ở __tests__/app/seo-metadata.test.tsx
    await expect(generateMetadata(props({ q: 'pho' }))).resolves.toMatchObject({ title: 'Tìm kiếm: pho' });
    await expect(generateMetadata(props())).resolves.toMatchObject({ title: 'Tìm kiếm công thức' });
  });
});
