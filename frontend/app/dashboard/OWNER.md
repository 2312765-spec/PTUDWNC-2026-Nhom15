# app/dashboard — **C** (recipes) + **B** (categories)

| Route | Quyền | Người |
|---|---|---|
| `/dashboard` | Author/Admin | C |
| `/dashboard/recipes` | Author/Admin | C — công thức của tôi, `GET /recipes?mine=true&status=` (D81) |
| `/dashboard/recipes/new` | Author/Admin | C — **wizard nhiều bước** |
| `/dashboard/recipes/[id]/edit` | **Owner** hoặc Admin | C |
| `/dashboard/categories` | **Admin** | B |

Middleware Next.js chỉ để UX. **Backend vẫn phải chặn lại** — không tin frontend
(`docs/permissions.md` mục 4).
