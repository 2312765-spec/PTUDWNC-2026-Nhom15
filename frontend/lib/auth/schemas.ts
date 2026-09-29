import { z } from 'zod';

/**
 * FR-AUTH-001 — khớp RegisterCommandValidator của backend.
 * D5: chỉ có email, password, displayName (KHÔNG có fullName/userName).
 * Password theo NFR-SEC-001 (decisions.md, mục "Ràng buộc validator").
 * Validate ở client chỉ để phản hồi nhanh — backend vẫn là nguồn quyết định (CONS-008).
 */
export const passwordRules = [
  { id: 'length', label: 'Ít nhất 8 ký tự', test: (v: string) => v.length >= 8 },
  { id: 'upper', label: 'Ít nhất 1 chữ hoa', test: (v: string) => /[A-Z]/.test(v) },
  { id: 'lower', label: 'Ít nhất 1 chữ thường', test: (v: string) => /[a-z]/.test(v) },
  { id: 'digit', label: 'Ít nhất 1 chữ số', test: (v: string) => /[0-9]/.test(v) },
  { id: 'special', label: 'Ít nhất 1 ký tự đặc biệt', test: (v: string) => /[^A-Za-z0-9]/.test(v) },
] as const;

export const registerSchema = z
  .object({
    displayName: z
      .string()
      .trim()
      .min(2, 'Tên hiển thị cần ít nhất 2 ký tự.')
      .max(100, 'Tên hiển thị tối đa 100 ký tự.'),
    email: z.string().trim().min(1, 'Vui lòng nhập email.').email('Email không đúng định dạng.'),
    password: z
      .string()
      .refine((v) => passwordRules.every((r) => r.test(v)), 'Mật khẩu chưa đủ mạnh.'),
    confirmPassword: z.string().min(1, 'Vui lòng nhập lại mật khẩu.'),
  })
  .refine((data) => data.password === data.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Mật khẩu nhập lại không khớp.',
  });

export type RegisterFormValues = z.infer<typeof registerSchema>;

/**
 * FR-AUTH-002 — khớp LoginCommandValidator: email đúng định dạng, password không rỗng.
 * KHÔNG áp rule độ mạnh mật khẩu ở đây (tài khoản cũ có thể đặt trước khi rule đổi).
 */
export const loginSchema = z.object({
  email: z.string().trim().min(1, 'Vui lòng nhập email.').email('Email không đúng định dạng.'),
  password: z.string().min(1, 'Vui lòng nhập mật khẩu.'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
