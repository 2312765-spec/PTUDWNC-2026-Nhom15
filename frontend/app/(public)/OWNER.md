# app/(public) — người phụ trách: **B**

Route group công khai, không cần đăng nhập. SRS mục 5.1:

| Route | Rendering | FR |
|---|---|---|
| `/recipes` | SSR (dynamic) | FR-RCP-001 |
| `/recipes/[slug]` | `export const revalidate = 300` | FR-RCP-002 |
| `/categories` | `export const revalidate = 3600` | FR-CAT-001 |
| `/categories/[slug]` | `export const revalidate = 600` | FR-CAT-002 |
| `/search` | SSR (`dynamic = 'force-dynamic'`, `?q=&page=`) | FR-SRCH-001 |

Trang chủ `/` đã có sẵn ở `app/(home)/page.tsx` (ISR 3600; route group `(home)` để `loading.tsx` chỉ áp cho trang chủ).
Server Component mặc định — chỉ thêm `'use client'` khi thực sự cần state/effect.

**Route có `notFound()` (`/recipes/[slug]`, `/categories/[slug]`) không được có `loading.tsx`** ở chính
segment đó hay segment cha: `loading.tsx` bật streaming, Next gửi header 200 trước khi `notFound()` chạy
→ soft 404 (NFR-SEO). Skeleton cho trang danh sách thì đặt trong route group riêng, như
`app/(home)/loading.tsx` và `app/categories/(list)/loading.tsx`. E2E `e2e/recipe.spec.ts` kiểm tra status 404.
