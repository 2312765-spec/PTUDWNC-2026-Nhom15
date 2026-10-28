# Plan — NFR-SEO-001/002: JSON-LD Recipe, meta tags, Open Graph, canonical

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-10-28.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `feature/NFR-SEO-001-002-seo-frontend`. Slice S11 (roadmap).

---

## 1. Phạm vi

| NFR | Nội dung (SRS §4.7) | Hiện trạng trước khi làm |
|---|---|---|
| NFR-SEO-001 | JSON-LD Schema.org `Recipe`: name, description, image, author, datePublished, prepTime, cookTime, totalTime, recipeYield, recipeIngredient[], recipeInstructions[], nutrition. Google Rich Results Test pass 100% | Viết inline trong `app/(public)/recipes/[slug]/page.tsx`. **Thiếu `datePublished`**. Không có ảnh → `image = "/placeholder-recipe.jpg"` (URL tương đối — Google báo lỗi). Thiếu `fiber`/`sodium`. Chưa có test riêng |
| NFR-SEO-002 | `<title>` "{Recipe} \| Culinary Blog" ≤ 60 ký tự · meta description 150–160 · og:title/description/image 1200×630/url/type · Twitter `summary_large_image` · canonical · robots index (Published) / noindex (Draft/Archived) | **Title lặp đuôi:** page tự ghép `\| Culinary Blog` trong khi `layout.tsx` có template `%s \| Culinary Blog`. Description = nguyên `recipe.description`. Thiếu `og:url`, Twitter card, canonical (`metadataBase` chưa có). og:image là ảnh gốc, kích thước bất kỳ. robots theo status đã đúng |

**Ngoài phạm vi:** `aggregateRating` / `video` (rating ngoài scope v1), 301 redirect slug (D13), sitemap/robots.txt
(NFR-SEO-003 — đã xong ở FR-JOB-003), biến thể ảnh 1200×630 ở backend (xem D61 mục 1), Lighthouse CI (NFR-PERF-005,
slice riêng).

## 2. Quyết định D-x áp dụng

| D-x | Ảnh hưởng |
|---|---|
| D13 | Slug bất biến → canonical luôn là `/recipes/{slug}`, không cần redirect |
| D19 | `cookTime = 0` hợp lệ → JSON-LD xuất `PT0M` |
| D52 | `NEXT_PUBLIC_SITE_URL` là gốc URL tuyệt đối của site (đã dùng ở `app/robots.ts`) |
| D58 | URL ảnh trả về từ API đã là URL công khai tuyệt đối (`Minio:PublicBaseUrl`) |

## 3. Quyết định mới — **D61** (thêm vào `docs/decisions.md` TRƯỚC khi code)

1. **og:image 1200×630** sinh ở frontend bằng `app/(public)/recipes/[slug]/opengraph-image.tsx` (`ImageResponse`
   của `next/og`): ảnh chính crop cover + lớp phủ tên món + tên site. Không có ảnh, hoặc ảnh không đọc được
   (WebP/AVIF Satori không hỗ trợ, lỗi mạng) → thẻ chữ thương hiệu. Không thêm biến thể ảnh ở backend
   (tránh migration + đổi DTO).
2. **Meta description 150–160 ký tự:** mô tả ngắn hơn 150 → nối thêm "Chuẩn bị X phút, nấu Y phút, Z phần ·
   {Danh mục}."; dài hơn 160 → cắt tại ranh giới từ, thêm "…", tổng ≤ 160. Không chặn tác giả nhập.
3. **"Pass 100%" Rich Results** = **0 lỗi**. Cảnh báo thiếu `aggregateRating`/`video` chấp nhận được.
4. **`/search` là `noindex, follow`** (khuyến nghị của Google cho trang kết quả tìm kiếm nội bộ).
   `/auth/login`, `/auth/register` cũng `noindex`.
5. **Canonical không kèm query param** (NFR-SEO-004: query chỉ dùng cho filter/sort/paging): `/recipes?page=2`
   → canonical `/recipes`.

## 4. File sẽ tạo / sửa

### Tạo mới (mảng D)

| File | Nội dung |
|---|---|
| `frontend/lib/seo/site.ts` | `getSiteUrl()` — đọc `NEXT_PUBLIC_SITE_URL`, bỏ `/` cuối, mặc định `http://localhost:3000`. `app/robots.ts` dùng lại |
| `frontend/lib/seo/recipeMeta.ts` | Hàm thuần: `buildRecipeTitle()` (tên ≤ 44 ký tự + đuôi template 16 = ≤ 60), `buildRecipeDescription()` (D61.2), `buildRecipeMetadata()` → `Metadata` (canonical, OG, Twitter, robots) |
| `frontend/lib/seo/recipeJsonLd.ts` | `buildRecipeJsonLd(recipe, siteUrl)`: bổ sung `datePublished`, `url`, `image` mảng URL tuyệt đối (ảnh gốc + ảnh OG — luôn có ít nhất 1), `HowToStep.url` (`#buoc-n`) + `image` của bước, `fiberContent`/`sodiumContent`, `recipeCategory`, `keywords`. Bỏ trường rỗng |
| `frontend/components/seo/RecipeJsonLd.tsx` | Server component render `<script type="application/ld+json">` qua `serializeJsonLd` (đã chống XSS) |
| `frontend/app/(public)/recipes/[slug]/opengraph-image.tsx` | D61.1 |

### Sửa — **báo trước** (file của B / A / file dùng chung)

| File | Chủ | Thay đổi |
|---|---|---|
| `frontend/app/layout.tsx` | dùng chung | `metadataBase`, `openGraph.siteName` + `locale: 'vi_VN'`, `twitter.card: 'summary_large_image'` mặc định |
| `frontend/app/(public)/recipes/[slug]/page.tsx` | B | `generateMetadata` → `buildRecipeMetadata`; JSON-LD → `<RecipeJsonLd>`; `id="buoc-n"` cho từng bước; sửa title lặp |
| `frontend/app/page.tsx`, `app/(public)/recipes/page.tsx`, `app/categories/page.tsx`, `app/categories/[slug]/page.tsx` | B | `alternates.canonical` (D61.5) |
| `frontend/app/(public)/search/page.tsx` | B | canonical `/search` + `robots: noindex, follow` (D61.4) |
| `frontend/app/auth/login/page.tsx`, `app/auth/register/page.tsx` | A | `robots: noindex` |
| `frontend/app/robots.ts` | D | dùng `getSiteUrl()` |

## 5. Test (viết trước, xác nhận fail)

| File | Phủ |
|---|---|
| `__tests__/lib/seo/recipeMeta.test.ts` | NFR-SEO-002: title vừa 60 / tên dài cắt "…" / không lặp đuôi · description ngắn bổ sung, dài cắt đúng ranh giới từ, kết quả 150–160 · canonical tuyệt đối · og:url, og:type, Twitter card · Published → index, Draft/Archived → noindex |
| `__tests__/lib/seo/recipeJsonLd.test.ts` | NFR-SEO-001: đủ 12 thuộc tính SRS · `image` luôn tuyệt đối, kể cả khi không có ảnh · `PT0M` khi `cookTime = 0` (D19) · `datePublished` · nutrition rỗng → bỏ trường · `HowToStep` có `position`, `url` |
| `__tests__/components/seo/RecipeJsonLd.test.tsx` | `</script>` trong title không thoát khỏi thẻ script |
| `__tests__/lib/seo/site.test.ts` | bỏ `/` cuối, mặc định localhost |
| `__tests__/app/recipes/[slug]/page.test.tsx` (sửa) | trang render `<RecipeJsonLd>`, anchor `#buoc-n` |
| `__tests__/app/robots.test.ts` (giữ xanh) | sau khi chuyển sang `getSiteUrl()` |

**Kiểm tay:** Google Rich Results Test chế độ dán mã HTML (localhost không public) → 0 lỗi · xem `/recipes/{slug}/opengraph-image`
ra PNG 1200×630 · Lighthouse SEO trang chi tiết.

## 6. Thứ tự

1. Báo B (sửa trang `(public)`), A (trang auth) và cả nhóm (`layout.tsx`).
2. `npm ci` (node_modules đang rỗng) → đọc `node_modules/next/dist/docs/` phần metadata, `opengraph-image`,
   `ImageResponse` của Next 16 trước khi code (`frontend/AGENTS.md`).
3. D61 vào `decisions.md`.
4. Test trước → `recipeMeta`, `recipeJsonLd`, `site` → `RecipeJsonLd` → `opengraph-image` → sửa các trang.
5. `npm run lint && npm run build && npm test` — 0 lỗi.
6. Kiểm tay (mục 5) → cập nhật `traceability.md` (NFR-SEO-001/002 ⬜ → ✅), `CHANGELOG.md` → commit
   `feat(NFR-SEO-001)`, `feat(NFR-SEO-002)`.

## 7. Rủi ro

| Rủi ro | Xử lý |
|---|---|
| Satori không decode WebP/AVIF (upload cho phép cả hai) | Fallback thẻ chữ (D61.1) — og:image vẫn hợp lệ 1200×630. Có test cho nhánh fallback |
| `opengraph-image` fetch ảnh qua domain công khai từ trong container | Đã giải quyết ở D58.4 (alias `nginx` = `SITE_DOMAIN`, `NODE_EXTRA_CA_CERTS`); lỗi mạng → fallback |
| API Next 16 khác Next 15 (metadata, `next/og`) | Đọc docs trong `node_modules` trước (bước 2) |
| Sửa file của B đụng nhánh B đang làm | Báo trước, merge trong ngày |
