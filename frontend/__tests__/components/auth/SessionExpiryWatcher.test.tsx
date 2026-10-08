import { render } from '@testing-library/react';
import { signOut, useSession } from 'next-auth/react';
import { SessionExpiryWatcher } from '@/components/auth/SessionExpiryWatcher';

jest.mock('next-auth/react', () => ({
  useSession: jest.fn(),
  signOut: jest.fn(),
}));
const mockUseSession = useSession as jest.Mock;
const mockSignOut = signOut as jest.Mock;

function sessionWith(error?: string) {
  return {
    status: 'authenticated',
    data: { expires: '2026-10-07T10:00:00Z', error },
    update: jest.fn(),
  };
}

beforeEach(() => {
  mockUseSession.mockReset();
  mockSignOut.mockReset();
});

describe('SessionExpiryWatcher — FR-AUTH-004', () => {
  it('FR-AUTH-004: session.error = RefreshTokenError → đăng xuất và về /auth/login?error=SessionExpired', () => {
    mockUseSession.mockReturnValue(sessionWith('RefreshTokenError'));

    render(<SessionExpiryWatcher />);

    expect(mockSignOut).toHaveBeenCalledTimes(1);
    expect(mockSignOut).toHaveBeenCalledWith({ redirectTo: '/auth/login?error=SessionExpired' });
  });

  it('FR-AUTH-004: render lại với cùng lỗi → không gọi signOut lần hai', () => {
    mockUseSession.mockReturnValue(sessionWith('RefreshTokenError'));

    const { rerender } = render(<SessionExpiryWatcher />);
    rerender(<SessionExpiryWatcher />);

    expect(mockSignOut).toHaveBeenCalledTimes(1);
  });

  it('FR-AUTH-004: phiên bình thường → không làm gì', () => {
    mockUseSession.mockReturnValue(sessionWith());

    render(<SessionExpiryWatcher />);

    expect(mockSignOut).not.toHaveBeenCalled();
  });

  it('FR-AUTH-004: chưa đăng nhập → không làm gì', () => {
    mockUseSession.mockReturnValue({ status: 'unauthenticated', data: null, update: jest.fn() });

    render(<SessionExpiryWatcher />);

    expect(mockSignOut).not.toHaveBeenCalled();
  });

  it('FR-AUTH-004: không render gì ra giao diện', () => {
    mockUseSession.mockReturnValue(sessionWith());

    const { container } = render(<SessionExpiryWatcher />);

    expect(container).toBeEmptyDOMElement();
  });
});
