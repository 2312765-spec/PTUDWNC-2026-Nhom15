import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { signOut } from 'next-auth/react';
import { LogoutButton } from '@/components/auth/LogoutButton';

jest.mock('next-auth/react', () => ({ signOut: jest.fn() }));
const mockSignOut = signOut as jest.Mock;

beforeEach(() => {
  jest.resetAllMocks();
});

describe('LogoutButton — FR-AUTH-005', () => {
  it('FR-AUTH-005: bấm → signOut chuyển về /auth/login?loggedOut=1', async () => {
    mockSignOut.mockResolvedValue(undefined);
    render(<LogoutButton />);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }));

    await waitFor(() =>
      expect(mockSignOut).toHaveBeenCalledWith({ redirectTo: '/auth/login?loggedOut=1' }),
    );
  });

  it('FR-AUTH-005/NFR-USE-004: đang đăng xuất → nút loading (aria-busy) và bị khóa, chống bấm đúp', async () => {
    let finish!: () => void;
    mockSignOut.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    render(<LogoutButton />);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }));

    const button = await screen.findByRole('button', { name: 'Đăng xuất' });
    await waitFor(() => expect(button).toHaveAttribute('aria-busy', 'true'));
    expect(button).toBeDisabled();
    expect(screen.getByText('Đang đăng xuất…')).toBeInTheDocument();

    finish();
    await waitFor(() => expect(button).not.toBeDisabled());
  });
});
