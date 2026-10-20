'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Button, Input, Textarea } from '@/components/ui';
import { toProblemDetails } from '@/lib/api-client';
import { profileSchema, type ProfileFormValues } from '@/lib/auth/schemas';
import { useUpdateProfile } from '@/lib/hooks/useUpdateProfile';
import type { UserProfile } from '@/lib/types';

const formFields = ['displayName', 'avatarUrl', 'bio'] as const;
type FormField = (typeof formFields)[number];

/** Backend trả key theo PropertyName của FluentValidation ("displayName", "avatarUrl"…). */
function toFormField(key: string): FormField | undefined {
  return formFields.find((f) => f.toLowerCase() === key.toLowerCase());
}

export interface EditProfileFormProps {
  profile: UserProfile;
  onDone: () => void;
}

/**
 * FR-AUTH-007 (D5, D54) — form sửa hồ sơ. Luôn gửi đủ 3 field: để trống avatarUrl/bio nghĩa là xóa
 * (chuỗi rỗng), không phải "giữ nguyên". NFR-USE-003: lỗi 400 hiện inline theo field.
 */
export function EditProfileForm({ profile, onDone }: EditProfileFormProps) {
  const [formError, setFormError] = useState<string | null>(null);
  const mutation = useUpdateProfile();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      displayName: profile.displayName,
      avatarUrl: profile.avatarUrl ?? '',
      bio: profile.bio ?? '',
    },
  });

  const onSubmit = (values: ProfileFormValues) => {
    setFormError(null);
    mutation.mutate(values, {
      onSuccess: onDone,
      onError: (err) => {
        const problem = toProblemDetails(err);

        if (problem.errors) {
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
      },
    });
  };

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      aria-label="Chỉnh sửa hồ sơ"
      className="flex flex-col gap-4"
    >
      {formError && (
        <p role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {formError}
        </p>
      )}

      <Input label="Tên hiển thị" error={errors.displayName?.message} {...register('displayName')} />

      <Input
        label="Đường dẫn ảnh đại diện (tùy chọn)"
        type="url"
        placeholder="https://example.com/anh.png"
        error={errors.avatarUrl?.message}
        {...register('avatarUrl')}
      />

      <Textarea label="Giới thiệu (tùy chọn)" rows={4} error={errors.bio?.message} {...register('bio')} />

      <div className="flex justify-end gap-2 border-t border-border pt-4">
        <Button type="button" variant="outline" onClick={onDone} disabled={mutation.isPending}>
          Hủy
        </Button>
        <Button type="submit" loading={mutation.isPending}>
          Lưu thay đổi
        </Button>
      </div>
    </form>
  );
}
