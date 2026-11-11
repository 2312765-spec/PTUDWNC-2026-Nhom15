import { render, screen } from '@testing-library/react';
import { usePathname } from 'next/navigation';
import { SidebarSearch } from '@/components/layout/SidebarSearch';

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn() }),
  usePathname: jest.fn(),
}));

const mockUsePathname = usePathname as jest.Mock;

describe('SidebarSearch (D97, FR-SRCH-001)', () => {
  it('trang thường → hiện ô tìm kiếm', () => {
    mockUsePathname.mockReturnValue('/recipes');
    render(<SidebarSearch />);

    expect(screen.getByRole('search')).toBeInTheDocument();
  });

  it('/search → ẩn (trang đó có ô riêng giữ ?q=), mỗi trang chỉ một vùng search', () => {
    mockUsePathname.mockReturnValue('/search');
    render(<SidebarSearch />);

    expect(screen.queryByRole('search')).not.toBeInTheDocument();
  });
});
