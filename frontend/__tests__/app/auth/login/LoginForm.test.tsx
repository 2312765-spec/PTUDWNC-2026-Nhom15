import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { loginAction, type LoginResult } from '@/app/auth/login/actions';
import { LoginForm } from '@/app/auth/login/LoginForm';
import type { ProblemDetails } from '@/lib/api-client';
import { mockPush } from '../../../../jest.setup';
import { renderWithProviders } from '../../../utils/render';

jest.mock('@/app/auth/login/actions', () => ({ loginAction: jest.fn() }));
const mockLogin = loginAction as jest.MockedFunction<typeof loginAction>;

const mockUpdateSession = jest.fn();
const mockSignIn = jest.fn();
jest.mock('next-auth/react', () => ({
  useSession: () => ({ data: null, status: 'unauthenticated', update: mockUpdateSession }),
  signIn: (...args: unknown[]) => mockSignIn(...args),
}));

const success: LoginResult = { ok: true };
const failure = (problem: ProblemDetails): LoginResult => ({ ok: false, problem });
/** Server action không tới được server Next (mất mạng) → fetch ném TypeError. */
const networkError = () => new TypeError('Failed to fetch');

function setup(props: { callbackUrl?: string } = {}) {
  const user = userEvent.setup();
  renderWithProviders(<LoginForm {...props} />);
  const fields = {
    email: screen.getByLabelText('Email'),
    password: screen.getByLabelText('Mật khẩu'),
  };
  const submit = screen.getByRole('button', { name: 'Đăng nhập' });

  async function fillValid(email = ' lan@example.com ', password = 'Lan@2026x') {
    await user.type(fields.email, email);
    await user.type(fields.password, password);
  }

  return { user, fields, submit, fillValid };
}

beforeEach(() => {
  mockLogin.mockReset();
  mockUpdateSession.mockReset();
  mockSignIn.mockReset();
});

describe('LoginForm — hiển thị & truy cập (NFR-USE-002)', () => {
  it('FR-AUTH-002: chỉ có ô email và mật khẩu, autocomplete đúng cho trình quản lý mật khẩu', () => {
    const { fields } = setup();
    expect(screen.getAllByRole('textbox')).toHaveLength(1);
    expect(fields.email).toHaveAttribute('autocomplete', 'email');
    expect(fields.password).toHaveAttribute('autocomplete', 'current-password');
  });

  it('FR-AUTH-002: nút hiện/ẩn đổi type của ô mật khẩu và aria-pressed', async () => {
    const { user, fields } = setup();
    expect(fields.password).toHaveAttribute('type', 'password');

    await user.click(screen.getByRole('button', { name: 'Hiện mật khẩu' }));
    expect(fields.password).toHaveAttribute('type', 'text');
    expect(screen.getByRole('button', { name: 'Ẩn mật khẩu' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
  });

  it('FR-AUTH-002/NFR-USE-002: Tab đi qua form theo đúng thứ tự', async () => {
    const { user, fields, submit } = setup();
    const toggle = screen.getByRole('button', { name: 'Hiện mật khẩu' });
    for (const el of [fields.email, fields.password, toggle, submit]) {
      await user.tab();
      expect(el).toHaveFocus();
    }
  });
});

describe('LoginForm — validation phía client', () => {
  it('FR-AUTH-002: submit form trống → lỗi dưới từng ô, không gọi API', async () => {
    const { user, fields, submit } = setup();
    await user.click(submit);

    expect(await screen.findByText('Vui lòng nhập email.')).toBeInTheDocument();
    expect(screen.getByText('Vui lòng nhập mật khẩu.')).toBeInTheDocument();
    expect(fields.password).toHaveAttribute('aria-invalid', 'true');
    expect(mockLogin).not.toHaveBeenCalled();
  });

  it('FR-AUTH-002: rời ô email sai định dạng → báo lỗi ngay (onBlur)', async () => {
    const { user, fields } = setup();
    await user.type(fields.email, 'lan@');
    await user.tab();
    expect(await screen.findByText('Email không đúng định dạng.')).toBeInTheDocument();
  });

  it('FR-AUTH-002: mật khẩu yếu vẫn gửi được (rule độ mạnh chỉ dành cho đăng ký)', async () => {
    mockLogin.mockResolvedValue(success);
    const { user, submit, fillValid } = setup();
    await fillValid('lan@example.com', 'abc');
    await user.click(submit);
    await waitFor(() => expect(mockLogin).toHaveBeenCalledTimes(1));
  });
});

describe('LoginForm — đăng nhập thành công', () => {
  it('FR-AUTH-002: gửi đúng { email đã trim, password }', async () => {
    mockLogin.mockResolvedValue(success);
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    await waitFor(() => expect(mockLogin).toHaveBeenCalledTimes(1));
    expect(mockLogin.mock.calls[0][0]).toEqual({ email: 'lan@example.com', password: 'Lan@2026x' });
  });

  it('FR-AUTH-002/NFR-USE-004: trong lúc gửi, nút bị khóa và có aria-busy', async () => {
    let resolve!: (value: LoginResult) => void;
    mockLogin.mockReturnValue(new Promise((r) => (resolve = r)));
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const busy = await screen.findByRole('button', { name: /Đang đăng nhập/ });
    expect(busy).toBeDisabled();
    expect(busy).toHaveAttribute('aria-busy', 'true');

    await act(async () => resolve(success));
  });

  it('FR-AUTH-002/NFR-USE-004: thành công → nạp lại phiên, toast có tên, chuyển sang /dashboard', async () => {
    mockLogin.mockResolvedValue(success);
    mockUpdateSession.mockResolvedValue({ profile: { displayName: 'Bếp của Lan' } });
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const toast = await screen.findByRole('status');
    expect(toast).toHaveTextContent('Đăng nhập thành công');
    expect(toast).toHaveTextContent('Chào mừng trở lại, Bếp của Lan!');
    expect(mockUpdateSession).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });

  it('FR-AUTH-002: có callbackUrl → quay lại đúng trang đó', async () => {
    mockLogin.mockResolvedValue(success);
    const { user, submit, fillValid } = setup({ callbackUrl: '/dashboard/recipes/new' });
    await fillValid();
    await user.click(submit);

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith('/dashboard/recipes/new'));
  });
});

describe('LoginForm — lỗi từ server (NFR-USE-003, D4, D17)', () => {
  it('FR-AUTH-002/A1: 401 → thông báo chung, không gắn lỗi vào ô email, xóa + focus ô mật khẩu', async () => {
    mockLogin.mockResolvedValue(
      failure({ type: 'AUTH_INVALID_CREDENTIALS', title: 'Unauthorized', status: 401 }),
    );
    const { user, fields, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(await screen.findByRole('alert')).toHaveTextContent('Email hoặc mật khẩu không đúng.');
    expect(fields.email).not.toHaveAttribute('aria-invalid', 'true');
    expect(fields.password).toHaveValue('');
    expect(fields.password).toHaveFocus();
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('FR-AUTH-002/A2/D17: 423 AUTH_ACCOUNT_LOCKED → hiện detail có số phút còn lại', async () => {
    const detail = 'Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau khoảng 12 phút.';
    mockLogin.mockResolvedValue(
      failure({ type: 'AUTH_ACCOUNT_LOCKED', title: 'Locked', status: 423, detail }),
    );
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(await screen.findByRole('alert')).toHaveTextContent(detail);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('FR-AUTH-002/D11: 403 AUTH_ACCOUNT_DISABLED → báo tài khoản bị vô hiệu hóa', async () => {
    mockLogin.mockResolvedValue(
      failure({ type: 'AUTH_ACCOUNT_DISABLED', title: 'Forbidden', status: 403 }),
    );
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(await screen.findByRole('alert')).toHaveTextContent('Tài khoản đã bị vô hiệu hóa.');
  });

  it('FR-AUTH-002/D4: 400 VALIDATION_ERROR với key PascalCase → lỗi đúng dưới từng ô', async () => {
    mockLogin.mockResolvedValue(
      failure({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        errors: { Email: ["'Email' is not a valid email address."] },
      }),
    );
    const { user, fields, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(await screen.findByText("'Email' is not a valid email address.")).toBeInTheDocument();
    expect(fields.email).toHaveAttribute('aria-invalid', 'true');
  });

  it('FR-AUTH-002: 500 → khung lỗi chung + toast', async () => {
    mockLogin.mockResolvedValue(
      failure({ type: 'INTERNAL_ERROR', title: 'Lỗi hệ thống', status: 500 }),
    );
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent === 'Lỗi hệ thống')).toBe(true);
    expect(alerts.some((a) => a.textContent?.includes('Đăng nhập thất bại'))).toBe(true);
  });

  it('FR-AUTH-002: mất mạng → thông báo NETWORK_ERROR; gửi lại thì khung lỗi cũ biến mất', async () => {
    mockLogin.mockRejectedValueOnce(networkError()).mockResolvedValueOnce(success);
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);
    const message = 'Vui lòng kiểm tra kết nối mạng và thử lại.';
    expect(await screen.findByText(message)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }));
    await waitFor(() => expect(screen.queryByText(message)).not.toBeInTheDocument());
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });
});

describe('LoginForm — Google (FR-AUTH-003, D9)', () => {
  it('có nút "Tiếp tục với Google" bên cạnh form email/mật khẩu', () => {
    setup();
    expect(screen.getByRole('button', { name: /Tiếp tục với Google/ })).toBeInTheDocument();
  });

  it('nhấn nút → gọi signIn("google", { callbackUrl }) — mặc định /dashboard', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: /Tiếp tục với Google/ }));
    expect(mockSignIn).toHaveBeenCalledWith('google', { callbackUrl: '/dashboard' });
  });

  it('có callbackUrl riêng → truyền đúng callbackUrl đó cho signIn', async () => {
    const { user } = setup({ callbackUrl: '/dashboard/recipes/new' });
    await user.click(screen.getByRole('button', { name: /Tiếp tục với Google/ }));
    expect(mockSignIn).toHaveBeenCalledWith('google', { callbackUrl: '/dashboard/recipes/new' });
  });

  it('D33: showGoogleError=true (Auth.js redirect về kèm ?error= sau khi profile() lỗi) → toast lỗi chung', async () => {
    renderWithProviders(<LoginForm showGoogleError />);
    const toast = await screen.findByRole('alert');
    expect(toast).toHaveTextContent('Đăng nhập Google thất bại');
  });

  it('showGoogleError=false (mặc định) → không có toast lỗi Google nào', () => {
    setup();
    expect(screen.queryByText('Đăng nhập Google thất bại')).not.toBeInTheDocument();
  });
});

describe('LoginForm — phiên hết hạn (FR-AUTH-004)', () => {
  it('FR-AUTH-004: showSessionExpired=true → toast thông tin (không phải lỗi) mời đăng nhập lại', async () => {
    renderWithProviders(<LoginForm showSessionExpired />);

    const toast = await screen.findByRole('status');
    expect(toast).toHaveTextContent('Phiên đăng nhập đã hết hạn');
    expect(toast).toHaveTextContent('Vui lòng đăng nhập lại');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('FR-AUTH-004: showSessionExpired=true → dọn ?error= khỏi URL, giữ callbackUrl', async () => {
    // jest.setup.ts mock useRouter() với replace mới mỗi lần gọi — thay tạm để bắt được lời gọi.
    const navigation = jest.requireMock('next/navigation') as Record<string, unknown>;
    const originalUseRouter = navigation.useRouter;
    const replace = jest.fn();
    navigation.useRouter = () => ({
      push: mockPush,
      replace,
      back: jest.fn(),
      prefetch: jest.fn(),
    });

    try {
      renderWithProviders(<LoginForm showSessionExpired callbackUrl="/dashboard/recipes/new" />);
      await screen.findByRole('status');
      expect(replace).toHaveBeenCalledWith('/auth/login?callbackUrl=%2Fdashboard%2Frecipes%2Fnew', {
        scroll: false,
      });
    } finally {
      navigation.useRouter = originalUseRouter;
    }
  });

  it('FR-AUTH-004: mặc định (không hết phiên) → không có toast phiên hết hạn', () => {
    setup();
    expect(screen.queryByText('Phiên đăng nhập đã hết hạn')).not.toBeInTheDocument();
  });
});
