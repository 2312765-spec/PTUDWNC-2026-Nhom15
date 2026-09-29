import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { registerAction, type RegisterResult } from '@/app/auth/register/actions';
import { RegisterForm } from '@/app/auth/register/RegisterForm';
import type { ProblemDetails } from '@/lib/api-client';
import { mockPush } from '../../../../jest.setup';
import { renderWithProviders } from '../../../utils/render';

jest.mock('@/app/auth/register/actions', () => ({ registerAction: jest.fn() }));
const mockRegister = registerAction as jest.MockedFunction<typeof registerAction>;

const mockUpdateSession = jest.fn();
jest.mock('next-auth/react', () => ({
  useSession: () => ({ data: null, status: 'unauthenticated', update: mockUpdateSession }),
}));

const success: RegisterResult = { ok: true };
const failure = (problem: ProblemDetails): RegisterResult => ({ ok: false, problem });
/** Server action không tới được server Next (mất mạng) → fetch ném TypeError. */
const networkError = () => new TypeError('Failed to fetch');

function setup() {
  const user = userEvent.setup();
  renderWithProviders(<RegisterForm />);
  const fields = {
    displayName: screen.getByLabelText('Tên hiển thị'),
    email: screen.getByLabelText('Email'),
    password: screen.getByLabelText('Mật khẩu'),
    confirmPassword: screen.getByLabelText('Nhập lại mật khẩu'),
  };
  const submit = screen.getByRole('button', { name: 'Đăng ký' });

  async function fillValid(overrides: Partial<Record<keyof typeof fields, string>> = {}) {
    const values = {
      displayName: '  Bếp của Lan ',
      email: 'lan@example.com',
      password: 'Lan@2026x',
      confirmPassword: 'Lan@2026x',
      ...overrides,
    };
    for (const [key, value] of Object.entries(values)) {
      await user.type(fields[key as keyof typeof fields], value);
    }
  }

  return { user, fields, submit, fillValid };
}

beforeEach(() => {
  mockRegister.mockReset();
  mockUpdateSession.mockReset();
});

describe('RegisterForm — hiển thị & truy cập (NFR-USE-002)', () => {
  it('FR-AUTH-001/D5: có đủ 4 ô, không có ô họ tên hay username', () => {
    setup();
    expect(screen.getAllByRole('textbox')).toHaveLength(2); // displayName + email
    expect(screen.queryByLabelText(/họ tên|username|tên đăng nhập/i)).not.toBeInTheDocument();
  });

  it('FR-AUTH-001/NFR-SEC-001: checklist mật khẩu cập nhật khi gõ', async () => {
    const { user, fields } = setup();
    const rules = screen.getByRole('list', { name: 'Yêu cầu mật khẩu' });
    expect(rules).toHaveTextContent(/Ít nhất 1 chữ hoa — chưa đạt/);

    await user.type(fields.password, 'A');
    expect(rules).toHaveTextContent(/Ít nhất 1 chữ hoa — đã đạt/);
    expect(rules).toHaveTextContent(/Ít nhất 8 ký tự — chưa đạt/);
  });

  it('FR-AUTH-001: nút hiện/ẩn đổi type của ô mật khẩu và aria-pressed', async () => {
    const { user, fields } = setup();
    const toggle = screen.getByRole('button', { name: 'Hiện mật khẩu' });
    expect(fields.password).toHaveAttribute('type', 'password');

    await user.click(toggle);
    expect(fields.password).toHaveAttribute('type', 'text');
    expect(fields.confirmPassword).toHaveAttribute('type', 'text');
    expect(screen.getByRole('button', { name: 'Ẩn mật khẩu' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
  });

  it('FR-AUTH-001/NFR-USE-002: Tab đi qua form theo đúng thứ tự', async () => {
    const { user, fields, submit } = setup();
    const toggle = screen.getByRole('button', { name: 'Hiện mật khẩu' });
    const order = [
      fields.displayName,
      fields.email,
      fields.password,
      toggle,
      fields.confirmPassword,
      submit,
    ];
    for (const el of order) {
      await user.tab();
      expect(el).toHaveFocus();
    }
  });
});

describe('RegisterForm — validation phía client', () => {
  it('FR-AUTH-001: submit form trống → lỗi dưới từng ô, không gọi API', async () => {
    const { user, fields, submit } = setup();
    await user.click(submit);

    expect(await screen.findByText('Tên hiển thị cần ít nhất 2 ký tự.')).toBeInTheDocument();
    expect(screen.getByText('Vui lòng nhập email.')).toBeInTheDocument();
    expect(screen.getByText('Mật khẩu chưa đủ mạnh.')).toBeInTheDocument();
    expect(screen.getByText('Vui lòng nhập lại mật khẩu.')).toBeInTheDocument();
    expect(fields.email).toHaveAttribute('aria-invalid', 'true');
    expect(mockRegister).not.toHaveBeenCalled();
  });

  it('FR-AUTH-001: rời ô email sai định dạng → báo lỗi ngay (onBlur)', async () => {
    const { user, fields } = setup();
    await user.type(fields.email, 'lan@');
    await user.tab();
    expect(await screen.findByText('Email không đúng định dạng.')).toBeInTheDocument();
  });

  it('FR-AUTH-001: mật khẩu nhập lại không khớp → không gọi API', async () => {
    const { user, submit, fillValid } = setup();
    await fillValid({ confirmPassword: 'Lan@2026y' });
    await user.click(submit);

    expect(await screen.findByText('Mật khẩu nhập lại không khớp.')).toBeInTheDocument();
    expect(mockRegister).not.toHaveBeenCalled();
  });
});

describe('RegisterForm — đăng ký thành công (D24)', () => {
  it('FR-AUTH-001/D5: gửi đúng { email, password, displayName } đã trim, không gửi confirmPassword', async () => {
    mockRegister.mockResolvedValue(success);
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    await waitFor(() => expect(mockRegister).toHaveBeenCalledTimes(1));
    expect(mockRegister.mock.calls[0][0]).toEqual({
      email: 'lan@example.com',
      password: 'Lan@2026x',
      displayName: 'Bếp của Lan',
    });
  });

  it('FR-AUTH-001/NFR-USE-004: trong lúc gửi, nút bị khóa và có aria-busy', async () => {
    let resolve!: (value: RegisterResult) => void;
    mockRegister.mockReturnValue(new Promise((r) => (resolve = r)));
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const busy = await screen.findByRole('button', { name: /Đang tạo tài khoản/ });
    expect(busy).toBeDisabled();
    expect(busy).toHaveAttribute('aria-busy', 'true');

    await act(async () => resolve(success));
  });

  it('FR-AUTH-001/D24: thành công → nạp lại phiên, toast chào mừng, chuyển sang /dashboard', async () => {
    mockRegister.mockResolvedValue(success);
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const toast = await screen.findByRole('status');
    expect(toast).toHaveTextContent('Đăng ký thành công');
    expect(toast).toHaveTextContent('Chào mừng Bếp của Lan!');
    expect(mockUpdateSession).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });
});

describe('RegisterForm — lỗi từ server (NFR-USE-003, D4)', () => {
  it('FR-AUTH-001/D4: 409 AUTH_EMAIL_EXISTS → lỗi dưới ô email, focus về email, không toast', async () => {
    mockRegister.mockResolvedValue(
      failure({ type: 'AUTH_EMAIL_EXISTS', title: 'Email đã tồn tại', status: 409 }),
    );
    const { user, fields, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(await screen.findByText('Email này đã được đăng ký.')).toBeInTheDocument();
    expect(fields.email).toHaveFocus();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('FR-AUTH-001/D4: 400 VALIDATION_ERROR với key PascalCase → lỗi đúng dưới từng ô', async () => {
    mockRegister.mockResolvedValue(
      failure({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        errors: {
          Password: ['Password phải có ít nhất 1 chữ thường.'],
          DisplayName: ['Tên hiển thị đã bị cấm.'],
        },
      }),
    );
    const { user, fields, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(await screen.findByText('Password phải có ít nhất 1 chữ thường.')).toBeInTheDocument();
    expect(screen.getByText('Tên hiển thị đã bị cấm.')).toBeInTheDocument();
    expect(fields.password).toHaveAttribute('aria-invalid', 'true');
    expect(fields.displayName).toHaveAttribute('aria-invalid', 'true');
  });

  it('FR-AUTH-001/D4: 400 VALIDATION_ERROR không khớp ô nào → khung lỗi chung + toast', async () => {
    mockRegister.mockResolvedValue(
      failure({
        type: 'VALIDATION_ERROR',
        title: 'Dữ liệu không hợp lệ',
        status: 400,
        detail: 'Yêu cầu không hợp lệ.',
        errors: { '': ['Body rỗng.'] },
      }),
    );
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent === 'Yêu cầu không hợp lệ.')).toBe(true);
    expect(alerts.some((a) => a.textContent?.includes('Đăng ký thất bại'))).toBe(true);
  });

  it('FR-AUTH-001/A4: 500 → khung lỗi chung + toast, không lộ chi tiết kỹ thuật', async () => {
    mockRegister.mockResolvedValue(
      failure({ type: 'INTERNAL_ERROR', title: 'Lỗi hệ thống', status: 500 }),
    );
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent === 'Lỗi hệ thống')).toBe(true);
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('FR-AUTH-001: mất mạng → thông báo NETWORK_ERROR', async () => {
    mockRegister.mockRejectedValue(networkError());
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);

    expect(
      await screen.findByText('Vui lòng kiểm tra kết nối mạng và thử lại.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Không kết nối được máy chủ')).toBeInTheDocument();
  });

  it('FR-AUTH-001: gửi lại sau lỗi thì khung lỗi cũ biến mất', async () => {
    mockRegister.mockRejectedValueOnce(networkError()).mockResolvedValueOnce(success);
    const { user, submit, fillValid } = setup();
    await fillValid();
    await user.click(submit);
    const message = 'Vui lòng kiểm tra kết nối mạng và thử lại.';
    expect(await screen.findByText(message)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Đăng ký' }));
    await waitFor(() => expect(screen.queryByText(message)).not.toBeInTheDocument());
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });
});
