# Plan — NFR-USE-001: Responsive Design

> 2026-11-08 · Người làm: C · Nhánh `feature/NFR-USE-001-responsive` · Quyết định: **D84**

## Yêu cầu (SRS NFR-USE-001)

Mobile 320–767px (1 cột, touch-friendly) · Tablet 768–1199px (lưới 2 cột) · Desktop ≥ 1200px (đầy đủ) · Tailwind
utility-first · thử bằng Chrome DevTools + BrowserStack (iOS, Android).

## Hiện trạng (đo 14 route × 320/375/768/1024/1200/1440, Playwright có cảm ứng)

| # | Phát hiện | File (chủ) |
|---|---|---|
| 1 | `/dashboard/categories` tràn ngang 84px @320 — cột Sửa/Xóa ra ngoài màn hình | `app/dashboard/categories/page.tsx` (B) |
| 2 | Trang sửa @320: hàng nút icon ép chữ bước/nguyên liệu thành mỗi dòng một từ | `RecipeStepsManager`, `RecipeIngredientsManager` (C) |
| 3 | Link header / "Xem tất cả →" … cao 19–20px < 24px | `SiteHeader` (C), `HeaderAuth` (A), vài trang |
| 4 | Lưới recipe 3 cột từ 1024px (`lg` mặc định) — SRS: tablet tới 1199px là 2 cột | token breakpoint (C) |

Ngoài phạm vi (báo B): trang chi tiết recipe không ảnh trỏ `/placeholder-recipe.jpg` nhưng không có `frontend/public` →
ảnh vỡ (FR-RCP-002).

## D84

1. `--breakpoint-lg` = 1200px (theo SRS). md giữ 768px.
2. Touch-friendly = vùng bấm ≥ 24×24px (WCAG 2.5.8); link trong đoạn văn được miễn; 44px chỉ khuyến nghị.
3. Bảng dữ liệu trên mobile được cuộn ngang trong khung riêng (WCAG 1.4.10), trang không cuộn ngang.
4. Không có BrowserStack → Playwright giả lập (có cảm ứng); thiết bị iOS/Android thật "chưa kiểm".

## File

| File | Thay đổi |
|---|---|
| `docs/decisions.md` | D84 |
| `frontend/app/globals.css` | `--breakpoint-lg: 75rem` |
| `components/layout/SiteHeader.tsx` | link ≥ 24px |
| `components/auth/HeaderAuth.tsx` (A) | link ≥ 24px |
| `RecipeStepsManager.tsx`, `RecipeIngredientsManager.tsx` | < 640px: nút xuống dưới nội dung |
| `app/dashboard/categories/page.tsx` (B) | `overflow-x-auto` + ẩn cột Slug < 640px |
| các link nhỏ khác | ≥ 24px |
| `frontend/e2e/responsive.spec.ts` (mới) | mọi route @320/768/1200: không cuộn ngang, vùng bấm ≥ 24px; lưới `/recipes` 1/2/2/3 cột @320/768/1024/1200 |
| `docs/traceability.md` | NFR-USE-001 → ✅ |

## Thứ tự

D84 → `responsive.spec.ts` chạy xác nhận fail → sửa đến khi pass → Jest, lint, build, E2E `--workers=1` → ảnh trước/sau →
commit.
