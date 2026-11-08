# app/dashboard — **C** (recipes) + **B** (categories)

| Route | Quyền | Người |
|---|---|---|
| `/dashboard` | Đăng nhập (tổng quan chỉ Author/Admin) | C — lời chào + tổng quan công thức của tôi (`AuthorOverview.tsx`, D82) + lối tắt |
| `/dashboard/recipes` | Author/Admin | C — công thức của tôi, `GET /recipes?mine=true&status=` (D81) |
| `/dashboard/recipes/new` | Author/Admin | C — **wizard nhiều bước** |
| `/dashboard/recipes/[id]/edit` | **Owner** hoặc Admin | C |
| `/dashboard/categories` | **Admin** | B |

Middleware Next.js chỉ để UX. **Backend vẫn phải chặn lại** — không tin frontend
(`docs/permissions.md` mục 4).
