'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { Eye, EyeOff, Lock } from 'lucide-react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Button, Input, useToast } from '@/components/ui';
import { toProblemDetails, type ProblemDetails } from '@/lib/api-client';
import type { LoginRequest } from '@/lib/auth/api';
import { loginSchema, type LoginFormValues } from '@/lib/auth/schemas';
import { cn } from '@/lib/utils';
import { GoogleSignInButton } from '../GoogleSignInButton';
import { loginAction } from './actions';

/** Lỗi backend trả về qua server action — mang nguyên ProblemDetails. */
class ProblemError extends Error {
  constructor(readonly problem: ProblemDetails) {
    super(problem.title);
  }
}

async function loginAndCreateSession(body: LoginRequest) {
  const result = await loginAction(body);
  if (!result.ok) throw new ProblemError(result.problem);
}

const formFields = ['email', 'password'] as const;
type ServerField = (typeof formFields)[number];

/** Backend trả key theo PropertyName của FluentValidation ("Email", "Password"). */
function toFormField(key: string): ServerField | undefined {
  return formFields.find((f) => f.toLowerCase() === key.toLowerCase());
}

interface FormError {
  message: string;
  /** D17 — tài khoản bị khóa hiển thị khác: người dùng cần chờ, không phải gõ lại. */
  locked?: boolean;
}

/**
 * FR-AUTH-002 — form đăng nhập.
 * A1: sai email/mật khẩu → một thông báo chung, không chỉ ra ô nào sai (chống User Enumeration).
 * A2/D17: 423 AUTH_ACCOUNT_LOCKED → hiện số phút còn lại lấy từ `detail`.
 * D11: 403 AUTH_ACCOUNT_DISABLED → tài khoản bị vô hiệu hóa.
 * NFR-USE-003: lỗi validation inline theo field. NFR-USE-004: nút loading, toast sau khi đăng nhập.
 */
export function LoginForm({
  callbackUrl = '/dashboard',
  showGoogleError = false,
  showSessionExpired = false,
}: {
  callbackUrl?: string;
  /** FR-AUTH-003 — true khi Auth.js redirect về đây kèm ?error= sau khi profile() của Google ném lỗi. */
  showGoogleError?: boolean;
  /** FR-AUTH-004 — true khi SessionExpiryWatcher đưa về đây kèm ?error=SessionExpired. */
  showSessionExpired?: boolean;
}) {
  const router = useRouter();
  const toast = useToast();
  const { update: refreshSession } = useSession();
  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState<FormError | null>(null);

  useEffect(() => {
    if (!showGoogleError && !showSessionExpired) return;
    toast.show(
      showSessionExpired
        ? {
            // Không phải lỗi của người dùng — thông tin, không dùng variant 'error'.
            variant: 'info',
            title: 'Phiên đăng nhập đã hết hạn',
            description: 'Vui lòng đăng nhập lại để tiếp tục.',
          }
        : {
            variant: 'error',
            title: 'Đăng nhập Google thất bại',
            description: 'Không thể xác thực với Google lúc này. Vui lòng thử lại.',
          },
    );
    // Dọn ?error= khỏi URL để refresh trang không hiện lại toast, giữ lại callbackUrl nếu có.
    const target =
      callbackUrl === '/dashboard'
        ? '/auth/login'
        : `/auth/login?callbackUrl=${encodeURIComponent(callbackUrl)}`;
    router.replace(target, { scroll: false });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showGoogleError, showSessionExpired]);

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    setFocus,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    mode: 'onBlur',
    defaultValues: { email: '', password: '' },
  });

  const mutation = useMutation({
    mutationFn: loginAndCreateSession,
    onSuccess: async () => {
      // Cookie phiên đã được server action đặt; nạp lại để SessionProvider thấy ngay.
      const session = await refreshSession();
      const name = session?.profile?.displayName;
      toast.show({
        variant: 'success',
        title: 'Đăng nhập thành công',
        description: name ? `Chào mừng trở lại, ${name}!` : 'Chào mừng trở lại!',
      });
      router.push(callbackUrl);
    },
    onError: (error) => {
      const problem = error instanceof ProblemError ? error.problem : toProblemDetails(error);

      switch (problem.type) {
        case 'AUTH_INVALID_CREDENTIALS':
          setValue('password', '');
          setFocus('password');
          setFormError({ message: 'Email hoặc mật khẩu không đúng.' });
          return;
        case 'AUTH_ACCOUNT_LOCKED':
          setFormError({
            locked: true,
            message:
              problem.detail ??
              'Tài khoản đang bị khóa tạm thời do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau.',
          });
          return;
        case 'AUTH_ACCOUNT_DISABLED':
          setFormError({ message: 'Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.' });
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

      setFormError({ message: problem.detail ?? problem.title });
      toast.show({ variant: 'error', title: 'Đăng nhập thất bại', description: problem.title });
    },
  });

  const onSubmit = ({ email, password }: LoginFormValues) => {
    setFormError(null);
    mutation.mutate({ email: email.trim(), password });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-5">
      {formError && (
        <div
          role="alert"
          className={cn(
            'flex items-start gap-2 rounded-md border p-3 text-sm',
            formError.locked
              ? 'border-amber-200 bg-amber-50 text-amber-900'
              : 'border-red-200 bg-red-50 text-red-800',
          )}
        >
          {formError.locked && <Lock className="mt-0.5 size-4 shrink-0" aria-hidden="true" />}
          <span>{formError.message}</span>
        </div>
      )}

      <Input
        label="Email"
        type="email"
        autoComplete="email"
        inputMode="email"
        placeholder="ban@example.com"
        error={errors.email?.message}
        {...register('email')}
      />

      <div className="relative">
        <Input
          label="Mật khẩu"
          type={showPassword ? 'text' : 'password'}
          autoComplete="current-password"
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

      <Button type="submit" size="lg" loading={mutation.isPending} className="mt-1 w-full">
        {mutation.isPending ? 'Đang đăng nhập…' : 'Đăng nhập'}
      </Button>

      <div className="relative flex items-center py-1 text-xs text-ink-muted" role="separator">
        <div className="flex-1 border-t border-border" />
        <span className="px-3">hoặc</span>
        <div className="flex-1 border-t border-border" />
      </div>

      <GoogleSignInButton callbackUrl={callbackUrl} />
    </form>
  );
}
