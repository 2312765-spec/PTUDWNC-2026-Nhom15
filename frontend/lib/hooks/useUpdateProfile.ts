import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSession } from 'next-auth/react';
import { useToast } from '@/components/ui';
import { updateMe, type UpdateProfileRequest } from '@/lib/auth/api';
import { currentUserQueryKey } from '@/lib/hooks/useCurrentUser';

/**
 * FR-AUTH-007 — cập nhật hồ sơ. Thành công: ghi đè cache ['auth','me'], đồng bộ tên/ảnh lên session
 * (header lấy từ JWT cookie) và toast xác nhận (NFR-USE-004). Lỗi để component form tự xử lý.
 */
export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const { update } = useSession();
  const toast = useToast();

  return useMutation({
    mutationFn: (body: UpdateProfileRequest) => updateMe(body),
    onSuccess: async (profile) => {
      queryClient.setQueryData(currentUserQueryKey, profile);
      toast.show({
        variant: 'success',
        title: 'Đã cập nhật hồ sơ',
        description: 'Thông tin của bạn đã được lưu.',
      });
      // Đồng bộ header là việc phụ: lỗi ở đây không được biến thành "lưu thất bại".
      try {
        await update({ profile });
      } catch {
        /* header sẽ tự cập nhật ở lần đăng nhập/làm mới phiên kế tiếp */
      }
    },
  });
}
