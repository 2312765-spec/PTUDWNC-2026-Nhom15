import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SearchPagination from '@/app/(public)/search/SearchPagination';
import { mockPush } from '../../../jest.setup';
import { renderWithProviders } from '../../utils/render';

// Điều khiển isPending của useTransition (jsdom chuyển trang tức thì nên không thấy được trạng thái đang tải).
let mockPending = false;
const mockStartTransition = jest.fn((callback: () => void) => callback());
jest.mock('react', () => ({
  ...jest.requireActual('react'),
  useTransition: () => [mockPending, mockStartTransition],
}));

beforeEach(() => {
  mockPending = false;
  mockStartTransition.mockClear();
});

describe('SearchPagination — FR-SRCH-004, NFR-USE-004', () => {
  it('FR-SRCH-004: bấm trang sau → chuyển tới ?q=&page= bên trong transition', async () => {
    renderWithProviders(<SearchPagination q="phở bò" page={1} totalPages={3} />);

    await userEvent.click(screen.getByRole('button', { name: 'Trang sau' }));

    expect(mockStartTransition).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith('/search?q=ph%E1%BB%9F%20b%C3%B2&page=2');
    expect(screen.getByRole('navigation')).toHaveAttribute('aria-busy', 'false');
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('NFR-USE-004/D86: đang chuyển trang → aria-busy, khóa nút, báo "Đang tải trang…" (không còn skeleton của Suspense)', () => {
    mockPending = true;

    renderWithProviders(<SearchPagination q="pho" page={2} totalPages={3} />);

    expect(screen.getByRole('navigation')).toHaveAttribute('aria-busy', 'true');
    for (const button of screen.getAllByRole('button')) expect(button).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Đang tải trang…');
  });
});
