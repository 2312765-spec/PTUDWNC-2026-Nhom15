/**
 * Nhãn + màu badge theo trạng thái recipe — dùng chung cho /dashboard (D82) và /dashboard/recipes (D81).
 * Trạng thái lạ rơi về Nháp ở nơi dùng.
 */
export const RECIPE_STATUS_BADGE: Record<string, { label: string; variant: 'default' | 'success' | 'warning' }> = {
  Draft: { label: 'Nháp', variant: 'warning' },
  Published: { label: 'Đã xuất bản', variant: 'success' },
  Archived: { label: 'Đã lưu trữ', variant: 'default' },
};
