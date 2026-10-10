# ADR-0004: Dùng SkiaSharp cho resize ảnh (thay SixLabors.ImageSharp)

- **Ngày:** 2026-10-10
- **Trạng thái:** Đã chấp nhận
- **Liên quan:** FR-JOB-002, CONS-007, CONS-009, NFR-MAINT-001, decisions.md D40, D41, D46

## Bối cảnh

FR-JOB-002 (PR #37) dùng `SixLabors.ImageSharp` 3.1.12 để sinh medium/thumbnail WebP. Sau khi
merge, CI đỏ ở `dotnet restore`: NuGetAudit báo 5 advisory (3 high, 2 moderate) và
`TreatWarningsAsErrors` (NFR-MAINT-001) biến chúng thành lỗi.

Cả 5 advisory chỉ được vá từ ImageSharp **4.1.2**. Đã thử nâng lên 4.1.3: build Debug chỉ cảnh
báo, nhưng build **Release** lỗi `No Six Labors license found` — ImageSharp 4.x bắt buộc license
key. CI (`ci.yml`) và Dockerfile (CONS-009) đều build Release.

## Quyết định

Thay ImageSharp bằng **SkiaSharp 3.119.4** (giấy phép MIT), kèm
`SkiaSharp.NativeAssets.Linux.NoDependencies` để chạy trên runner Ubuntu và image
`mcr.microsoft.com/dotnet/aspnet:10.0` mà không cần cài `libfontconfig`.

`Infrastructure/Files/SkiaImageResizer.cs` hiện thực `IImageResizer` với cùng hành vi D40/D41:

- `SKCodec` đọc header trước → định dạng lạ hoặc vượt 40 MP trả `null` mà chưa decode pixel.
- Decode sang sRGB, tự xoay theo `SKCodec.EncodedOrigin` (EXIF Orientation 2–8).
- Encode WebP quality 80 trên surface không gắn color space → không có EXIF/XMP/ICC trong đầu ra.
- Native Skia đóng gói sẵn không có decoder AVIF → ảnh gốc AVIF vẫn no-op (D41).

Application không đổi: chỉ biết `IImageResizer`.

## Phương án đã cân nhắc

1. **ImageSharp 4.1.3 + license key** — giữ nguyên code. Nhược: phải đăng ký key, đưa vào
   GitHub secret và build arg Docker; mọi thành viên build Release local cũng cần key.
2. **Giữ 3.1.12, suppress NU1902/NU1903** — nhanh nhất. Nhược: vẫn ship lỗ hổng thật;
   GHSA-gwg2-r3hj-4w44 nằm ở parse ICC profile, chạm tới được qua JPEG/PNG upload hợp lệ.
3. **Magick.NET / NetVips** — có thể đọc AVIF. Nhược: native lớn hơn nhiều, làm nặng image
   Docker và CI (đúng lý do D41 đã loại bỏ).
4. **SkiaSharp** (chọn) — MIT, không cần key, native gọn, có sẵn decoder JPEG/PNG/WebP và encoder WebP.

## Hệ quả

- CI restore/build Release xanh trở lại, không cần secret mới.
- SkiaSharp đóng gói native cho mọi nền tảng: publish không chỉ định RID ra ~480 MB (native win/mac/linux).
  Dockerfile vì vậy restore/publish với `-a $TARGETARCH` → thư mục publish ~47 MB, chỉ một
  `libSkiaSharp.so`.
- SkiaSharp không ghi được EXIF → test auto-orient tự chèn segment APP1 vào JPEG
  (`IntegrationTests/Files/TestImages.cs`).
- Nếu sau này cần decode AVIF thì phải đổi thư viện (phương án 3) và sửa D41.
