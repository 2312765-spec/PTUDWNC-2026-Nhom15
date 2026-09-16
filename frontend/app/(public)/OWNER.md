# app/(public) — người phụ trách: **B**

Route group công khai, không cần đăng nhập. SRS mục 5.1:

| Route | Rendering | FR |
|---|---|---|
| `/recipes` | SSR (dynamic) | FR-RCP-001 |
| `/recipes/[slug]` | `export const revalidate = 300` | FR-RCP-002 |
| `/categories` | `export const revalidate = 3600` | FR-CAT-001 |
| `/categories/[slug]` | `export const revalidate = 600` | FR-CAT-002 |

Trang chủ `/` đã có sẵn ở `app/page.tsx` (ISR 3600).
Server Component mặc định — chỉ thêm `'use client'` khi thực sự cần state/effect.
