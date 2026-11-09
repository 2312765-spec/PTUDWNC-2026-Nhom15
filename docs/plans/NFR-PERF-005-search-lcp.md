# NFR-PERF-005 / D86 — LCP của `/search` vượt 2.5s

> 2026-11-09. PR #86 (ảnh seed thật) làm job Lighthouse CI đỏ.

## Số liệu (báo cáo Lighthouse của runner CI, run 42593569015)

| URL | LCP 5 lần (ms) | Phần tử LCP | LCP quan sát thật |
|---|---|---|---|
| `/search?q=ga` | 2607 · 2671 · 2801 · 2874 · 2957 | ảnh card đầu | **194ms** (ảnh tải xong ở 40ms) |
| `/recipes` | 1965 · 1966 · 1969 · 2119 · 2439 | đoạn chữ trong header | 93ms |
| `/recipes/banh-khot` | 1888 · 1888 · 1889 · 1889 · 2506 | ảnh đầu trang | 73ms |

- Chỉ `/search` vượt ngưỡng ổn định. `/recipes` và `/recipes/banh-khot` fail ở các run trước vì lhci chọn
  trúng một lần đo lệch làm "run đại diện".
- `/search` bọc kết quả trong `<Suspense>`: server gửi kết quả trong `<div hidden id="S:0">`, script `$RC`/`$RV`
  của React mới cho hiện. React 19 gom và hoãn việc hiện boundary → ảnh được vẽ sau khi JS hydrate. Lighthouse
  mô phỏng (lantern) tính mọi request trước thời điểm LCP quan sát được → cả ~180 KB JS và các prefetch RSC.
- Trang chi tiết không stream (0 boundary) nên LCP là ảnh mà vẫn ~1.9s.

## Đã thử, không hiệu quả (giữ lại vì vô hại)

- `RecipeCard`: quality 50, chỉ card đầu eager + `fetchPriority="high"` — ảnh nhẹ ~30%, LCP không đổi rõ.
- Làm nóng cache trước khi đo (`frontend/scripts/lhci-server.mjs`) — API đã nhanh (search ~4ms).
- Artifact `lighthouse` giữ `.lighthouseci` (`include-hidden-files: true`) — cần để có số liệu trên.

## Sửa

| File | Thay đổi |
|---|---|
| `frontend/app/(public)/search/page.tsx` | Bỏ `<Suspense>` quanh `SearchResults`. Không có `loading.tsx` nào phía trên nên server chờ kết quả rồi gửi ngay trong HTML đầu tiên (không còn `<div hidden>`) |
| `frontend/app/(public)/search/SearchResults.tsx` | Giữ nguyên (vẫn là Server Component async tự gọi API) — chỉ cần bỏ boundary phía trên |
| `frontend/app/(public)/search/SearchPagination.tsx` | Chuyển trang trong `startTransition`; đang tải → `aria-busy` + mờ danh sách (NFR-USE-004) |
| `frontend/components/search/SearchForm.tsx` | Giữ nguyên (đã có "Đang tìm…") |
| `docs/decisions.md` | Viết lại D86 theo nguyên nhân thật |
| `docs/traceability.md` | Cập nhật ghi chú NFR-PERF-005 |

## Test

- Jest: cây JSX của trang `/search` không còn `<Suspense>` quanh kết quả; phân trang đang chuyển → `aria-busy`.
- E2E (`e2e/search.spec.ts`): HTML trả về của `/search?q=` chứa sẵn kết quả, không có `<div hidden id="S:`.
- CI Lighthouse: `/search?q=ga` ≤ 2500ms.

## Ngoài phạm vi

- `/recipes` dùng cùng mẫu Suspense nhưng đang pass (LCP mobile là chữ) — ghi chú trong D86 để theo dõi.
