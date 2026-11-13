# Plan — DEV-SEED: ảnh thật cho món ăn và danh mục seed

> 2026-11-08 · Người làm: C · Nhánh `chore/dev-seed-images` · Tiếp nối `docs/plans/DEV-SEED-du-lieu-mau-that.md`

## Phạm vi và truy vết

Dữ liệu mẫu Development (SRS dòng 295 — 50 recipe / 5 tác giả, Bogus) đang gắn ảnh giả `placehold.co` (Next còn chặn
→ lỗi 400 trên trang); 8 danh mục seed không có `ImageUrl`. Phục vụ hiển thị ảnh của FR-RCP-008 / FR-JOB-002 khi demo.
Không đổi entity, không migration, không mâu thuẫn SRS → không cần D mới.

## Nguồn ảnh

Wikimedia Commons, chỉ nhận **CC0 / Public Domain / CC BY / CC BY-SA**. Chọn bằng mắt từ bảng ảnh ứng viên (đúng món).
Món không có ảnh phù hợp → báo lại, không gắn bừa. Resize 1200×900 WebP. Ảnh danh mục = ảnh một món tiêu biểu của
danh mục. Ghi công từng ảnh trong `CREDITS.md` (bắt buộc với CC BY / BY-SA). User-Agent chung "CulinaryBlogSeed" + link
repo.

## File

| File | Thay đổi |
|---|---|
| `backend/CulinaryBlog.Infrastructure/Persistence/Seeding/Images/{slug}.webp` | ảnh, `EmbeddedResource` (chạy được trong Docker) |
| `.../Seeding/Images/CREDITS.md` | tác giả, giấy phép, link gốc từng ảnh |
| `CulinaryBlog.Infrastructure.csproj` | khai báo `EmbeddedResource` |
| `DbSeeder.cs` (B) | bỏ `placehold.co`; `EnsureSeedImagesAsync` chạy **cả khi đã seed**: recipe seed còn ảnh placeholder / chưa có ảnh → upload qua `IFileStorageService` → gắn ảnh → enqueue tạo thumbnail (`IBackgroundJobService`); danh mục seed chưa có `ImageUrl` → gán. Chạy lại không tạo trùng |
| `Program.cs` | truyền `IFileStorageService`, `IBackgroundJobService` cho seeder |

## Test (viết trước)

Integration test seeder: DB mới → mọi recipe seed có ảnh trên S3, không còn `placehold.co` · DB đã seed với ảnh
placeholder → thay ảnh · chạy lần 2 không tạo ảnh trùng · danh mục seed có `ImageUrl`.

## Kiểm chứng

Khởi động lại API trên máy dev, xem `/`, `/recipes`, trang chi tiết có ảnh thật; chụp màn hình.

**Lưu ý:** `DbSeeder` thuộc B → B review.

## Kết quả (2026-11-08)

- **44/50 món** có ảnh (44 file WebP, 4,15 MB). 6 món không tìm được ảnh đúng món trên Wikimedia Commons, để trống
  (không gắn ảnh sai): nem chua rán, tôm rim mặn ngọt, gỏi ngó sen tôm thịt, sữa chua nếp cẩm, chè ba màu, nước sâm bí đao.
- **Bỏ ảnh danh mục** khỏi phạm vi: DTO danh mục không trả `ImageUrl`, frontend không hiển thị ảnh danh mục ở đâu cả —
  thêm vào là tính năng mới (cần FR/D).
- Gỡ `placehold.co` khỏi `remotePatterns` và CSP `img-src` (`frontend/next.config.ts`) — không còn dùng.
- DB dev máy C: 44 ảnh placeholder đã được thay, Hangfire tạo đủ thumbnail.

## D98 — Thay ảnh bằng nguồn kết hợp (2026-11-13)

- **Lý do:** ảnh Commons phần lớn nghiệp dư, một số sai món (canh nấm chay = bò hầm Hàn Quốc), ảnh bị cắt xấu.
- **Nguồn:** Pexels (37), Unsplash (1), Wikimedia Commons (4 mới + 7 giữ lại). Không dùng ảnh AI. Chọn tay từ bảng ứng
  viên (ảnh hiện tại đặt cạnh để so), kiểm tra lại cả khung 4:3 lẫn dải card 2,4:1.
- **Kết quả:** 49/50 món có ảnh; còn trống `goi-ngo-sen-tom-thit` (không có ảnh ngó sen đúng món).
- **DB đã seed:** `DbSeeder.FindRetiredSeedImageAsync` + `SeedImageAssets.RetiredSha256` — ảnh seed cũ (khớp SHA-256)
  được thay khi khởi động lại API, file cũ + biến thể bị xóa qua job (D43). Không cần xóa volume.
- **Test:** `DbSeederLegacyImagesTests` thêm 2 case D98 (ảnh cũ bị thay + xóa file; seed lần 2 không thay lại),
  fixture ảnh cũ `IntegrationTests/Seeding/Fixtures/retired-seed-image.webp`.
