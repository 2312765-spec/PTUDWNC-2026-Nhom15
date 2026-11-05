# Plan — NFR-PERF-005: Lighthouse CI (Core Web Vitals)

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-11-05.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `feature/NFR-PERF-005-lighthouse-ci`.

---

## 1. Phạm vi

| NFR | Nội dung (SRS §NFR-PERF-005) | Trạng thái |
|---|---|---|
| NFR-PERF-005 | Đo bằng Lighthouse CI: LCP ≤ 2.5s · CLS ≤ 0.1 · INP ≤ 200ms · First Load JS ≤ 200KB gzip. Kỹ thuật: ISR, `next/image`, code splitting | ⬜ Chưa làm |

**Ngoài phạm vi:** đo INP thật từ người dùng (RUM/CrUX — cần site public có traffic), trang
`/dashboard/*` (CSR sau đăng nhập, không index), trang `/auth/*`.

## 2. Quyết định D-x áp dụng

| D-x | Ảnh hưởng |
|---|---|
| D58 | Ảnh MinIO đi qua `next/image` với `remotePatterns` — dev: `localhost:9000` |
| D61 | Trang `/search` noindex nhưng vẫn đo hiệu năng (SRS 5.1: SSR) |

## 3. Quyết định mới — thêm vào `docs/decisions.md` TRƯỚC khi code

| D | Chủ đề | Chốt |
|---|---|---|
| **D64** | Chỉ số Lighthouse đo được | **INP → TBT ≤ 200ms.** Lighthouse chạy chế độ navigation, không có tương tác người dùng nên không có INP; TBT là chỉ số lab Google khuyên dùng thay INP. **Bundle → `resource-summary:script:size` ≤ 204800 byte** (tổng JS tải qua mạng, đã nén) trên mỗi trang — Next 16 không còn in "First Load JS" khi build. **Preset mobile** (mặc định Lighthouse: 4G chậm, CPU ×4). Mỗi URL chạy 3 lần, lấy trung vị |

## 4. Trang đo

Dữ liệu từ `DbSeeder` (Development, `RandomSeed` cố định → slug ổn định):

| URL | Rendering (SRS 5.1) |
|---|---|
| `/` | ISR 3600 |
| `/recipes` | SSR |
| `/recipes/banh-khot` | ISR 300 |
| `/categories` | ISR 3600 |
| `/categories/mon-khai-vi` | ISR 600 |
| `/search?q=ga` | SSR |

## 5. File tạo / sửa

| Tầng | File | Việc |
|---|---|---|
| Docs | `docs/decisions.md` | Thêm D64 |
| Frontend | `frontend/package.json` | devDependency `@lhci/cli` (ghim version), script `"lhci": "lhci autorun"` |
| Frontend | `frontend/lighthouserc.json` | `collect`: `startServerCommand: npm run start`, 6 URL, `numberOfRuns: 3`. `assert`: LCP ≤ 2500, CLS ≤ 0.1, TBT ≤ 200, script ≤ 204800 (mức `error`). `upload`: `filesystem` vào `.lighthouseci/` — **không** dùng `temporary-public-storage` |
| Frontend | `frontend/.gitignore` (hoặc root) | Bỏ `.lighthouseci/` |
| CI | `.github/workflows/ci.yml` | Job `lighthouse`: `docker compose up -d postgres redis minio` → `dotnet run` API (`ASPNETCORE_ENVIRONMENT=Development` để seed) → chờ `/health/ready` → `npm ci && npm run build` → `npm run lhci` → upload artifact `.lighthouseci/` |
| Docs | `docs/traceability.md` | NFR-PERF-005 → ✅ kèm số đo thật |
| Docs | `CHANGELOG.md` | Mục Added |

Nếu trang nào không đạt ngưỡng, sửa trong phạm vi NFR này (vd. ảnh LCP thiếu `priority`/`sizes`,
khung ảnh thiếu kích thước gây CLS, component client kéo bundle lớn) — ghi rõ trong commit.

## 6. Test

Ngưỡng `assert` của Lighthouse chính là test. Bước Test first: chạy `npm run lhci` trên bản build
hiện tại (backend Development chạy sẵn) → ghi số đo nền vào mục 8, xác nhận assert hoạt động
(trang vượt ngưỡng thì lhci thoát mã ≠ 0).

## 7. Rủi ro

- Job CI dựng cả backend → thêm ~6–10 phút.
- Điểm Lighthouse trên runner dao động → 3 lần chạy, trung vị.
- API Development cần secret `Jwt:Key` (`JwtKeyGuard`) → đặt biến môi trường khóa giả trong job CI.

## 8. Kết quả đo

### Đo nền 2026-11-05 (máy local Windows, Lighthouse 12 qua `@lhci/cli` 0.15.1, trung vị 3 lần)

| Trang | LCP (ms) | CLS | TBT (ms) | JS (KB) | Kết quả |
|---|---|---|---|---|---|
| `/` | 2647 | 0 | 171 | 189 | ✗ LCP |
| `/recipes` | 2385 | 0 | 107 | 196 | ✓ |
| `/recipes/banh-khot` | 2364 | 0 | 56 | 196 | ✓ |
| `/categories` | 2798 | 0 | 117 | 189 | ✗ LCP |
| `/categories/mon-khai-vi` | 2316 | 0 | 84 | 193 | ✓ |
| `/search?q=ga` | 2184 | 0 | 101 | 195 | ✓ |

**Phân tích:**

- Phần tử LCP ở mọi trang là **chữ** (h1/p), không phải ảnh; không có font web. 83% LCP là
  Render Delay. Trace thật (không throttle) FCP = LCP; con số 2.2–2.8s là của mô hình giả lập
  Lantern — nó cộng thời gian tải mọi JS được yêu cầu trước mốc LCP quan sát trên 4G chậm, nên
  **LCP tỷ lệ với dung lượng JS**.
- JS ~190KB/trang: react-dom 70 · Next runtime 43 (sàn, không bỏ được) · **axios + polyfill
  `Buffer` 20** · next-auth/TanStack 9 · lucide 10 · còn lại < 10 mỗi chunk.
- Chunk axios có ở cả 6 trang: `/categories` dùng trực tiếp (`DeleteCategoryButton` — hiện cả
  với Guest); 5 trang còn lại do `<Link>` prefetch route `/auth/login`, `/categories` (LoginForm
  import `toProblemDetails` từ `api-client` → kéo axios).
- Mốc vẽ thật dao động 0.2s ↔ 1.3s giữa các lần chạy (máy local đang chạy Docker + API) →
  cần số đo trên runner CI để kết luận.

### Tối ưu 1 — cắt axios khỏi trang public (duyệt 2026-11-05)

- `lib/problem-details.ts`: `ProblemDetails` + `toProblemDetails` không import axios (kiểm cờ
  `isAxiosError` thay `instanceof AxiosError`); `api-client.ts` re-export để code cũ giữ nguyên.
  7 file chỉ cần đọc lỗi (form/action auth, hồ sơ, `credentials.ts`) import thẳng module mới.
- `components/categories/AdminDeleteCategoryButton.tsx`: trên `/categories` public, nút xóa chỉ
  render cho Admin (`session.profile.roles`) và tải `DeleteCategoryButton` qua `next/dynamic`.
  Guest/Author không còn thấy nút xóa (đúng `permissions.md`). `/dashboard/categories` giữ nguyên.
- Kiểm tĩnh sau build: chunk axios chỉ còn trong manifest `/dashboard/categories`,
  `/dashboard/profile`.
- Test: `__tests__/lib/problem-details.test.ts` (4), `__tests__/components/categories/AdminDeleteCategoryButton.test.tsx` (3).

**Đo lại sau tối ưu 1 (máy local, cùng ngày):**

| Trang | LCP (ms) | TBT (ms) | CLS | JS (KB) — trước → sau |
|---|---|---|---|---|
| `/` | 2748 | 272 | 0 | 189 → **169** |
| `/recipes` | 2634 | 274 | 0 | 196 → **176** |
| `/recipes/banh-khot` | 2779 | 149 | 0 | 196 → **176** |
| `/categories` | 2554 | 217 | 0 | 189 → **169** |
| `/categories/mon-khai-vi` | 2666 | 200 | 0 | 193 → **174** |
| `/search?q=ga` | 2859 | 142 | 0 | 195 → **176** |

JS giảm đúng 20KB mọi trang (số đo xác định, không phụ thuộc máy). LCP/TBT lần này **không so
được** với lần đo nền: `benchmarkIndex` của Lighthouse dao động **1030–2199** giữa 18 lần chạy
(CPU laptop chênh > 2 lần trong cùng phiên; container gần như 0% CPU), TBT cùng một trang lệch
132 ↔ 399ms. Máy local không đủ ổn định để kết luận ngưỡng ±200ms → cần số đo trên runner CI.

### Phụ: `/categories` ISR khi build không có API

`next build` không có API (image prod) làm `/categories` thành ƒ (SSR) vì `noStore()`. Sửa: lúc
build (`NEXT_PHASE`) render trạng thái lỗi → route luôn ISR 3600. Kiểm: build với API chết →
`○ /categories 1h`. Test `__tests__/app/categories/page.test.tsx` (4).
