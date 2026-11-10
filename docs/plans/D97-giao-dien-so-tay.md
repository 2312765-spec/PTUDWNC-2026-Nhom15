# Kế hoạch — D97: giao diện "sổ tay nấu ăn", tông tối (NFR-USE-001/002)

**Ngày:** 2026-11-10 · **Branch:** `feature/D97-giao-dien-so-tay` · **Người làm:** D
**Mockup đã chốt:** https://claude.ai/artifact/9B9gVDKQ5wujwRTP9wrBUJ (trang chủ, chi tiết, điện thoại, phương án icon B)
**Phạm vi:** chỉ frontend. Không đổi backend / API.
**Điều kiện trước:** C đồng ý — plan sửa design token, `components/ui/*`, header (sở hữu C) và trang của B/C.

## 1. Hiện trạng (Explore)

- Màu đi chủ yếu qua token `app/globals.css` (~370 chỗ `text-ink`, `bg-surface`, `brand-*`…) → đổi giá trị token là
  phần lớn app chuyển tông. Còn ~95 class màu cứng, nặng nhất `app/(public)/recipes/[slug]/page.tsx` (47:
  `bg-white`, `gray-*`), phần còn lại rải ở `ui/*`, dashboard, auth.
- `SiteHeader` là Server Component tĩnh, không gọi `auth()` để giữ ISR (SRS 5.1) — thanh bên mới giữ ràng buộc này.
- Bước có `timerMinutes` **nullable** → dòng thời gian cần dự phòng.
- Lý do: đề tài chung cho > 30 nhóm; bố cục mặc định (header ngang → hero → lưới thẻ 3 cột) dễ trùng.

## 2. Việc làm

| # | Phần | File chính | Nội dung |
|---|---|---|---|
| 0 | Quyết định | `docs/decisions.md` | **D97** — bộ nhận diện: màu tối, 2 font, icon niêu đất, bố cục sổ tay; truy về NFR-USE-001/002 |
| 1 | Token, font, icon | `app/globals.css`, `app/layout.tsx`, `app/icon.svg` | Token tối: nền `#1A1310`, bề mặt `#241A16`, viền `#3A2C25`, chữ `#F7EBDD` / phụ `#CDB8A6`, brand `#E8693F` (chữ trên nút brand dùng màu nền tối), vàng `#F2B544`; `color-scheme: dark`; bảng contrast từng cặp ≥ 4.5:1 (viền ô nhập ≥ 3:1). Font *Baloo 2* (700/800, tiêu đề) + *Be Vietnam Pro* (400/600/700) qua **`next/font`**, subset `vietnamese` + `latin`, `display: swap`. Favicon niêu đất |
| 2 | Bỏ màu cứng | ~28 file | `bg-white`/`gray-*`/`orange-*` → token; màu lỗi `red-*` → token `danger` mới |
| 3 | Thanh bên thay header | `components/layout/SiteSidebar.tsx` (thay `SiteHeader`), `app/layout.tsx` | Logo, ô tìm kiếm, điều hướng, **Mục lục** (danh mục đánh số từ `getCategories()`; lỗi → ẩn mục, không sập trang), `HeaderAuth` cuối. < 1200px: thanh trên + nút "Mục lục" bằng `<details>` (không JS, dùng bàn phím được). **Giữ chữ link** "Công thức", "Danh mục", "Tìm kiếm" và `aria-label="Điều hướng chính"` |
| 4 | Trang chủ | `app/(home)/page.tsx`, `components/recipes/RecipeMenuRow.tsx` (mới) | Tiêu đề → **Món đáng thử** (recipe mới nhất đầu tiên, ảnh `medium`, preload — phần tử LCP mới) → **Thực đơn mới**: danh sách số thứ tự + tên + đường chấm dẫn + thời gian, ảnh `thumbnail` 88px. Danh mục chuyển sang thanh bên |
| 5 | Trang chi tiết | `app/(public)/recipes/[slug]/page.tsx` | Tiêu đề lớn, thông số một dòng, ảnh rộng; **Dòng thời gian nấu** cộng dồn `timerMinutes` (có bước thiếu → mốc "Bước n" thay "phút x"); nguyên liệu "Đi chợ" có ô tick ở cột phải (màn nhỏ: trên các bước). **Giữ neo `#buoc-n`** cho JSON-LD (NFR-SEO-001) |
| 6 | Trang còn lại | `/recipes`, `/search`, `/categories/*`, dashboard, auth | Chỉ đổi màu qua token — giữ bố cục lưới thẻ / bộ lọc / phân trang |
| 7 | Kiểm tra | | Unit (home, chi tiết, sidebar); E2E toàn bộ; `a11y.spec` 16 route 0 vi phạm; `responsive.spec` 320/768/1200; **Lighthouse 6 URL** (LCP ≤ 2.5s, JS ≤ 200KB); chụp màn hình app thật so với mockup |
| 8 | Tài liệu | `traceability.md`, `CHANGELOG.md` | NFR-USE-001/002, NFR-PERF-005; mục Changed |

## 3. Commit

1. (0+1+2) `feat(NFR-USE-002): token tông tối, font, icon niêu đất (D97)`
2. (3) `feat(NFR-USE-001): thanh bên mục lục thay header (D97)`
3. (4) `feat(FR-RCP-001): trang chủ dạng thực đơn (D97)`
4. (5) `feat(FR-RCP-002): trang chi tiết với dòng thời gian nấu (D97)`
5. (7+8) `test/docs`

Sau mỗi commit app vẫn chạy. Thiếu thời gian trước demo → dừng sau commit 2.

## 4. Rủi ro

1. **LCP** — 2 font + ảnh LCP mới ở trang chủ có thể phá kết quả D86–D88. Giảm: `next/font`, preload; bắt buộc đo lại.
2. **Fetch danh mục trong root layout** — chạy trên mọi trang: kiểm ISR còn giữ, build không có API vẫn chạy (bài học
   FR-CAT-001 ISR 3600).
3. **Test bám cấu trúc cũ** (lưới thẻ / khu danh mục trang chủ, header) phải sửa theo.
