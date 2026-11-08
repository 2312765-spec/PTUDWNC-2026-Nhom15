# Plan — NFR-USE-002: Accessibility (WCAG 2.1 AA)

> 2026-11-08 · Người làm: C · Nhánh `feature/NFR-USE-002-a11y` · Quyết định: **D83**

## Yêu cầu (SRS NFR-USE-002)

WCAG 2.1 AA: semantic HTML5 (`<article>`, `<nav>`, `<main>`, `<aside>`) · ARIA trên phần tử tương tác · mọi chức năng
dùng được bằng bàn phím (Tab, Enter, Escape) · tương phản ≥ 4.5:1 (chữ) và ≥ 3:1 (thành phần UI) · thử với NVDA và
VoiceOver.

## Hiện trạng (Explore)

- Đã có: skip link + `<main id="main">` chung (`app/layout.tsx`), viền focus 2px toàn cục, tab WAI-ARIA ở
  `/dashboard/recipes`, `<dialog>` gốc (focus trap + Esc), nút Lên/Xuống thay kéo-thả bước, skeleton `aria-busy`.
- axe (wcag2a/aa, wcag21a/aa) trên 16 trang: **14/16 lỗi `color-contrast`** — chữ trắng trên `brand-600` #ea580c
  (3.55:1); `/recipes/[slug]` thêm `text-orange-600` trên nền kem (3.42:1). `/dashboard`, `/dashboard/profile` sạch.
- Đo tay (axe không kiểm): viền input `border` #e7e5e4 trên trắng 1.26:1 < 3:1 (WCAG 1.4.11).

## D83

1. Screen reader: **chỉ NVDA** — nhóm không có Mac/iPhone. VoiceOver ghi "chưa kiểm".
2. Cách đo: axe-core trong E2E (0 vi phạm) + checklist tay `docs/a11y-checklist.md` (bàn phím, NVDA, 1.4.11).
3. Sửa SRS: NFR-USE-002 bỏ VoiceOver khỏi phạm vi v1.

## Sửa tương phản — đổi giá trị token (C sở hữu design token)

| Token | Cũ | Mới | Tương phản |
|---|---|---|---|
| `brand-600` | #ea580c | #c2410c | chữ trắng 5.18:1 · viền focus 5.18:1 |
| `brand-700` | #c2410c | #9a3412 | nút hover / link ≈ 7.3:1 |
| `border-strong` (mới) | — | #78716c | viền input/select/textarea ≈ 4.8:1 |

Một chỗ sửa, 13 file dùng `bg-brand-600` tự đúng — không đụng file của A/B/D.

## File

| File | Thay đổi |
|---|---|
| `docs/decisions.md` | D83 + dòng bảng tra nhanh |
| `frontend/package.json` | devDependency `@axe-core/playwright` (khóa version) |
| `frontend/app/globals.css` | 2 token màu + `border-strong` |
| `frontend/components/ui/{Input,Textarea,Select}.tsx` | `border-border` → `border-border-strong` |
| `frontend/app/(public)/recipes/[slug]/page.tsx` | `text-orange-600` → `text-brand-700` (file của B — B review) |
| `frontend/e2e/a11y.spec.ts` (mới) | Quét 16 trang, 0 vi phạm |
| `docs/a11y-checklist.md` (mới) | Checklist tay |
| `docs/traceability.md` | NFR-USE-002 → 🟡 (✅ sau checklist NVDA) |

Dữ liệu cho `a11y.spec.ts`: đăng ký tài khoản mới qua API, tạo + xuất bản recipe qua API; `/dashboard/categories` dùng
Admin seed, đăng nhập thất bại → `test.skip`.

## Thứ tự

1. D83 → 2. `a11y.spec.ts`, chạy xác nhận fail → 3. sửa token/file đến 0 vi phạm; `npm test`, lint, build, E2E
`--workers=1` → 4. checklist + traceability, commit → 5. người dùng chạy checklist NVDA.

**Rủi ro:** màu cam đậm hơn toàn site (chụp trước/sau để duyệt); test Jest kiểm class màu phải cập nhật.
