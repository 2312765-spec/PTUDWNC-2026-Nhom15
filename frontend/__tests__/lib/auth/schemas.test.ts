import { loginSchema, passwordRules, registerSchema } from '@/lib/auth/schemas';

const valid = {
  displayName: 'Bếp của Lan',
  email: 'lan@example.com',
  password: 'Lan@2026x',
  confirmPassword: 'Lan@2026x',
};

function fieldErrors(input: Record<string, unknown>) {
  const result = registerSchema.safeParse(input);
  return result.success ? {} : result.error.flatten().fieldErrors;
}

describe('registerSchema — FR-AUTH-001', () => {
  it('FR-AUTH-001/D5: dữ liệu hợp lệ thì pass', () => {
    expect(registerSchema.safeParse(valid).success).toBe(true);
  });

  it.each([
    ['1 ký tự', 'L'],
    ['101 ký tự', 'a'.repeat(101)],
    ['toàn khoảng trắng', '     '],
  ])('FR-AUTH-001/D5: displayName %s → lỗi', (_, displayName) => {
    expect(fieldErrors({ ...valid, displayName }).displayName).toBeDefined();
  });

  it('FR-AUTH-001/D5: displayName được trim', () => {
    const result = registerSchema.parse({ ...valid, displayName: '  Bếp của Lan  ' });
    expect(result.displayName).toBe('Bếp của Lan');
  });

  it.each([
    ['rỗng', ''],
    ['sai định dạng', 'lan@'],
  ])('FR-AUTH-001: email %s → lỗi', (_, email) => {
    expect(fieldErrors({ ...valid, email }).email).toBeDefined();
  });

  it.each([
    ['thiếu độ dài', 'La@1x'],
    ['thiếu chữ hoa', 'lan@2026x'],
    ['thiếu chữ thường', 'LAN@2026X'],
    ['thiếu chữ số', 'Lan@abcdx'],
    ['thiếu ký tự đặc biệt', 'Lan2026xy'],
  ])('FR-AUTH-001/NFR-SEC-001: password %s → lỗi', (_, password) => {
    expect(fieldErrors({ ...valid, password, confirmPassword: password }).password).toBeDefined();
  });

  it('FR-AUTH-001: confirmPassword không khớp → lỗi nằm ở confirmPassword', () => {
    const errors = fieldErrors({ ...valid, confirmPassword: 'Lan@2026y' });
    expect(errors.confirmPassword).toEqual(['Mật khẩu nhập lại không khớp.']);
    expect(errors.password).toBeUndefined();
  });

  it('FR-AUTH-001/NFR-SEC-001: password đúng 8 ký tự đủ điều kiện thì đạt mọi rule', () => {
    expect(passwordRules.every((r) => r.test('Aa1!aaaa'))).toBe(true);
    expect(passwordRules.find((r) => r.id === 'length')!.test('Aa1!aaa')).toBe(false);
  });

  it('FR-AUTH-001/D5: schema không có fullName hay userName', () => {
    expect(Object.keys(registerSchema.innerType().shape).sort()).toEqual(
      ['confirmPassword', 'displayName', 'email', 'password'],
    );
  });
});

describe('loginSchema — FR-AUTH-002', () => {
  function loginErrors(input: Record<string, unknown>) {
    const result = loginSchema.safeParse(input);
    return result.success ? {} : result.error.flatten().fieldErrors;
  }

  it('FR-AUTH-002: email + mật khẩu bất kỳ không rỗng thì pass (không áp rule độ mạnh)', () => {
    expect(loginSchema.safeParse({ email: ' lan@example.com ', password: 'abc' }).success).toBe(true);
  });

  it('FR-AUTH-002: thiếu email / mật khẩu → báo lỗi từng ô', () => {
    const errors = loginErrors({ email: '', password: '' });
    expect(errors.email?.[0]).toBe('Vui lòng nhập email.');
    expect(errors.password).toEqual(['Vui lòng nhập mật khẩu.']);
  });

  it('FR-AUTH-002: email sai định dạng → báo lỗi', () => {
    expect(loginErrors({ email: 'lan@', password: 'x' }).email).toEqual(['Email không đúng định dạng.']);
  });
});
