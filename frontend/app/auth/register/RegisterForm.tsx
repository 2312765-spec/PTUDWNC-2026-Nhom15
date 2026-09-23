'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { Check, Eye, EyeOff, X } from 'lucide-react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Button, Input, useToast } from '@/components/ui';
import { toProblemDetails, type ProblemDetails } from '@/lib/api-client';
import type { RegisterRequest } from '@/lib/auth/api';
import { passwordRules, registerSchema, type RegisterFormValues } from '@/lib/auth/schemas';
import { cn } from '@/lib/utils';
import { registerAction } from './actions';

/** Lỗi backend trả về qua server action — mang nguyên ProblemDetails. */
class ProblemError extends Error {
  constructor(readonly problem: ProblemDetails) {
    super(problem.title);
  }
}

async function registerAndSignIn(body: RegisterRequest) {
  const result = await registerAction(body);
  if (!result.ok) throw new ProblemError(result.problem);
}

const formFields = ['displayName', 'email', 'password'] as const;
type ServerField = (typeof formFields)[number];

/** Backend trả key theo PropertyName của FluentValidation ("Email", "DisplayName"…). */
function toFormField(key: string): ServerField | undefined {
  return formFields.find((f) => f.toLowerCase() === key.toLowerCase());
}

/**
 * FR-AUTH-001 — form đăng ký.
 * NFR-USE-003: lỗi hiển thị inline theo field, đọc từ ProblemDetails.errors; xử lý theo error code.
 * NFR-USE-004: nút có trạng thái loading, toast sau khi ghi.
 */
export function RegisterForm() {
  const router = useRouter();
  const toast = useToast();
  const { update: refreshSession } = useSession();
  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    mode: 'onBlur',
    defaultValues: { displayName: '', email: '', password: '', confirmPassword: '' },
  });

  const password = useWatch({ control, name: 'password' });

  const mutation = useMutation({
    mutationFn: registerAndSignIn,
    // D24 — cookie phiên đã được server action đặt; nạp lại để SessionProvider thấy ngay.
    onSuccess: async (_, { displayName }) => {
      await refreshSession();
      toast.show({
        variant: 'success',
        title: 'Đăng ký thành công',
        description: `Chào mừng ${displayName}! Email chào mừng đang được gửi tới bạn.`,
      });
      router.push('/dashboard');
    },
    onError: (error) => {
      const problem = error instanceof ProblemError ? error.problem : toProblemDetails(error);

      if (problem.type === 'AUTH_EMAIL_EXISTS') {
        setError('email', { message: 'Email này đã được đăng ký.' }, { shouldFocus: true });
        return;
      }

      if (problem.type === 'VALIDATION_ERROR' && problem.errors) {
        let mapped = false;
        for (const [key, messages] of Object.entries(problem.errors)) {
          const field = toFormField(key);
          if (field && messages[0]) {
            setError(field, { message: messages[0] });
            mapped = true;
          }
        }
        if (mapped) return;
      }

      setFormError(problem.detail ?? problem.title);
      toast.show({ variant: 'error', title: 'Đăng ký thất bại', description: problem.title });
    },
  });

  const onSubmit = ({ email, password, displayName }: RegisterFormValues) => {
    setFormError(null);
    mutation.mutate({ email: email.trim(), password, displayName: displayName.trim() });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-5">
      {formError && (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          {formError}
        </div>
      )}

      <Input
        label="Tên hiển thị"
        autoComplete="nickname"
        placeholder="Ví dụ: Bếp của Lan"
        hint="Tên này hiển thị trên các công thức bạn đăng."
        error={errors.displayName?.message}
        {...register('displayName')}
      />

      <Input
        label="Email"
        type="email"
        autoComplete="email"
        inputMode="email"
        placeholder="ban@example.com"
        error={errors.email?.message}
        {...register('email')}
      />

      <div className="flex flex-col gap-2">
        <div className="relative">
          <Input
            label="Mật khẩu"
            type={showPassword ? 'text' : 'password'}
            autoComplete="new-password"
            className="pr-10"
            error={errors.password?.message}
            {...register('password')}
          />
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
            aria-pressed={showPassword}
            className="absolute right-2 top-[33px] rounded p-1 text-ink-muted hover:text-ink"
          >
            {showPassword ? (
              <EyeOff className="size-4" aria-hidden="true" />
            ) : (
              <Eye className="size-4" aria-hidden="true" />
            )}
          </button>
        </div>

        <ul id="password-rules" className="grid grid-cols-1 gap-1 text-xs sm:grid-cols-2" aria-label="Yêu cầu mật khẩu">
          {passwordRules.map((rule) => {
            const ok = rule.test(password);
            const Icon = ok ? Check : X;
            return (
              <li key={rule.id} className={cn('flex items-center gap-1.5', ok ? 'text-green-700' : 'text-ink-muted')}>
                <Icon className="size-3.5 shrink-0" aria-hidden="true" />
                <span>
                  {rule.label}
                  <span className="sr-only">{ok ? ' — đã đạt' : ' — chưa đạt'}</span>
                </span>
              </li>
            );
          })}
        </ul>
      </div>

      <Input
        label="Nhập lại mật khẩu"
        type={showPassword ? 'text' : 'password'}
        autoComplete="new-password"
        error={errors.confirmPassword?.message}
        {...register('confirmPassword')}
      />

      <Button type="submit" size="lg" loading={mutation.isPending} className="mt-1 w-full">
        {mutation.isPending ? 'Đang tạo tài khoản…' : 'Đăng ký'}
      </Button>

      {/* FR-AUTH-003 — nút Google do slice S2 (A) gắn khi tích hợp Auth.js. */}
    </form>
  );
}
