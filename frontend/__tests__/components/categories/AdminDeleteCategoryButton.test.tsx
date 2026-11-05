import { render, screen } from '@testing-library/react';
import { useSession } from 'next-auth/react';
import { AdminDeleteCategoryButton } from '@/components/categories/AdminDeleteCategoryButton';
import type { UserProfile } from '@/lib/types';

jest.mock('next-auth/react', () => ({ useSession: jest.fn() }));
// next/dynamic trong jsdom: trả thẳng component đã mock, không cần chờ chunk
jest.mock('next/dynamic', () => () => {
  const Stub = ({ categoryName }: { categoryName: string }) => <button type="button">Xóa {categoryName}</button>;
  return Stub;
});
const mockUseSession = useSession as jest.Mock;

const author: UserProfile = {
  id: 'u1',
  email: 'author@example.com',
  displayName: 'Tác giả',
  avatarUrl: null,
  bio: null,
  roles: ['Author'],
};
const admin: UserProfile = { ...author, id: 'u2', roles: ['Author', 'Admin'] };

function renderButton() {
  render(<AdminDeleteCategoryButton categoryId="c1" categoryName="Món khai vị" recipeCount={0} />);
}

describe('AdminDeleteCategoryButton (FR-CAT-005, NFR-PERF-005/D64)', () => {
  it('FR-CAT-005: Guest không thấy nút xóa', () => {
    mockUseSession.mockReturnValue({ data: null, status: 'unauthenticated' });
    renderButton();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('FR-CAT-005: Author không thấy nút xóa', () => {
    mockUseSession.mockReturnValue({ data: { profile: author }, status: 'authenticated' });
    renderButton();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('FR-CAT-005: Admin thấy nút xóa', () => {
    mockUseSession.mockReturnValue({ data: { profile: admin }, status: 'authenticated' });
    renderButton();
    expect(screen.getByRole('button', { name: 'Xóa Món khai vị' })).toBeInTheDocument();
  });
});
