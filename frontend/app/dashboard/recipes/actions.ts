'use server';

import { updateTag } from 'next/cache';
import { auth } from '@/auth';

/** D72 — slug chỉ gồm a-z, 0-9 và một dấu `-` giữa các cụm; cắt 200 ký tự + hậu tố `-N`. */
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const SLUG_MAX_LENGTH = 210;

/**
 * D80 — xóa NGAY data cache của Next.js (ISR SRS 5.1) sau khi recipe đổi trạng thái hiển thị: publish / unpublish
 * (FR-RCP-005), archive (FR-RCP-006), delete (FR-RCP-007). Không có bước này Guest vẫn thấy recipe đã ẩn tới hết
 * chu kỳ ISR (trang chủ 1 giờ). `updateTag` hết hạn ngay (khác `revalidateTag(tag, 'max')` là stale-while-revalidate).
 *
 * Chỉ người đã đăng nhập mới gọi được (chống spam xóa cache); quyền thật vẫn do backend kiểm khi đổi trạng thái —
 * action này không đọc hay ghi dữ liệu nào, chỉ buộc lần render sau lấy bản mới từ API.
 */
export async function revalidateRecipeAction(slug: string): Promise<void> {
  const session = await auth();
  if (!session?.user) {
    return;
  }

  updateTag('recipes');
  if (typeof slug === 'string' && slug.length <= SLUG_MAX_LENGTH && SLUG_PATTERN.test(slug)) {
    updateTag(`recipe:${slug}`);
  }
}
