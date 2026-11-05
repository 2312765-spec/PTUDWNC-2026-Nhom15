'use client';

import dynamic from 'next/dynamic';
import { useSession } from 'next-auth/react';
import type { DeleteCategoryButtonProps } from '@/components/categories/DeleteCategoryButton';
import { CardFooter } from '@/components/ui';

// Tải riêng: DeleteCategoryButton kéo axios (~20KB) — Guest trên trang public không phải tải (NFR-PERF-005/D64).
const DeleteCategoryButton = dynamic(() =>
  import('@/components/categories/DeleteCategoryButton').then((m) => m.DeleteCategoryButton),
);

/**
 * FR-CAT-005 — chân thẻ danh mục kèm nút xóa trên trang public `/categories`, chỉ hiện với Admin
 * (docs/permissions.md). Chỉ là UX — backend vẫn chặn bằng AdminPolicy (NFR-SEC-006).
 */
export function AdminDeleteCategoryButton(props: DeleteCategoryButtonProps) {
  const { data: session } = useSession();
  if (!session?.profile?.roles.includes('Admin')) return null;

  return (
    <CardFooter className="flex justify-end p-2">
      <DeleteCategoryButton {...props} />
    </CardFooter>
  );
}
