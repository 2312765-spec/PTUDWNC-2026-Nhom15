# Plan — FR-JOB-003: Sitemap Generation Job

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-10-14.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `feature/FR-JOB-003-sitemap`.

---

## 1. Phạm vi

| FR / NFR | Nội dung | Trạng thái |
|---|---|---|
| FR-JOB-003 | Hangfire recurring, cron `0 2 * * *` (UTC). Sinh `sitemap.xml` gồm Published recipes + categories + trang tĩnh. Retry 2 lần. Log số URL qua Serilog | ⬜ Chưa làm |
| NFR-SEO-003 | sitemap.xml có `<loc>`, `<lastmod>`, `<changefreq>`, `<priority>` · robots.txt cho phép mọi crawler, khai báo Sitemap URL | ⬜ Chưa làm |

**Ngoài phạm vi:** sitemap index (> 50.000 URL — quy mô v1 không tới), sitemap ảnh, ping Google (D51),
301 redirect slug cũ (D13).

## 2. Quyết định D-x áp dụng

| D-x | Ảnh hưởng |
|---|---|
| D1 / D2 | Recipe / Category đã soft delete bị Global Query Filter loại — không vào sitemap |
| D13 | Slug bất biến — không cần lưu slug cũ, sitemap chỉ có slug hiện tại |
| D16 | Bucket public-read → Nginx proxy thẳng tới object, không cần presigned URL |
| D44 | Job chỉ chuyển tiếp sang MediatR command, logic nằm ở Application |
| D45 | Hangfire server đã bật (trừ `Testing`) |

## 3. Quyết định mới — thêm vào `docs/decisions.md` TRƯỚC khi code

| D | Chủ đề | Chốt |
|---|---|---|
| **D51** | Ping Google Search Console | **Bỏ.** Endpoint `google.com/ping?sitemap=` ngừng từ 06/2023 (trả 404). Google tìm sitemap qua dòng `Sitemap:` trong robots.txt |
| **D52** | Nơi lưu & phục vụ sitemap | MinIO, key cố định `seo/sitemap.xml`; Nginx `location = /sitemap.xml` proxy tới MinIO. Thêm `IFileStorageService.SaveAsync(objectKey, …)` — chỉ cho file hệ thống sinh. **Bỏ `frontend/app/sitemap.ts`**, frontend chỉ làm `app/robots.ts`. Danh sách URL: `/`, `/recipes`, `/categories`, category chưa xóa, recipe Published |
| **D53** | Chống chạy trùng | `[DisableConcurrentExecution]` của Hangfire (lock phân tán trên Postgres storage) thay RedLock |

## 4. Luồng

```
Hangfire cron 0 2 * * * UTC
  → SitemapGenerationJob            [AutomaticRetry(2)] [DisableConcurrentExecution(600)]
    → ISender.Send(GenerateSitemapCommand)
      → GenerateSitemapCommandHandler
          ISitemapRepository   → (slug, lastModified) recipes Published + categories
          SitemapXmlBuilder    → XML sitemaps.org 0.9
          IFileStorageService.SaveAsync("seo/sitemap.xml", …, "application/xml")
          log Information "{UrlCount}"
Nginx  /sitemap.xml  → minio:9000/culinary-blog/seo/sitemap.xml
Next   /robots.txt   → Sitemap: {NEXT_PUBLIC_SITE_URL}/sitemap.xml
```

| Loại URL | priority | changefreq | lastmod |
|---|---|---|---|
| `/` | 1.0 | daily | max lastmod recipe |
| `/recipes` | 0.9 | daily | max lastmod recipe |
| recipe | 0.8 | weekly | `UpdatedAt ?? PublishedAt ?? CreatedAt` |
| `/categories` | 0.7 | weekly | max lastmod category |
| category | 0.7 | weekly | `UpdatedAt ?? CreatedAt` |

Không có recipe/category nào → bỏ `<lastmod>` của trang tĩnh tương ứng.

## 5. File theo tầng

### Domain
- **Tạo** `Interfaces/ISitemapRepository.cs` — `GetPublishedRecipeEntriesAsync`, `GetCategoryEntriesAsync`
  trả `SitemapEntry(Slug, LastModified)`. Interface riêng để không đụng `IRecipeRepository` (C) /
  `ICategoryRepository` (B).

### Application
- **Sửa** `Common/Interfaces/IFileStorageService.cs` — thêm `SaveAsync(objectKey, content, contentType, ct)`,
  ghi đè. **Hợp đồng chung — báo C.**
- **Tạo** `Seo/Commands/GenerateSitemap/GenerateSitemapCommand.cs` — `IRequest<int>` (số URL).
- **Tạo** `Seo/Commands/GenerateSitemap/GenerateSitemapCommandHandler.cs` — đọc `Seo:SiteBaseUrl`;
  thiếu / không phải URL tuyệt đối http(s) → `InvalidOperationException` (Hangfire retry → Failed).
- **Tạo** `Seo/SitemapXmlBuilder.cs` — hàm thuần, `XmlWriter`, ngày W3C, số theo `InvariantCulture`.
- Command **không** implement `ICacheInvalidator` — không cache nào đổi.

### Infrastructure
- **Tạo** `Repositories/SitemapRepository.cs` — projection + `AsNoTracking`.
- **Sửa** `Files/MinioFileStorageService.cs` — `SaveAsync` qua `PutObjectAsync` với key truyền vào.
- **Tạo** `Jobs/SitemapGenerationJob.cs`.
- **Tạo** `Jobs/RecurringJobsRegistration.cs` — `RegisterRecurringJobs(IServiceProvider)`:
  `AddOrUpdate("sitemap-generation", …, "0 2 * * *", TimeZone = UTC)`.
- **Sửa** `DependencyInjection.cs` — đăng ký `ISitemapRepository`, `SitemapGenerationJob`.

### API
- **Sửa** `Program.cs` — `app.Services.RegisterRecurringJobs()` trong nhánh `!Testing`. **File dùng chung — báo A.**
- **Sửa** `appsettings.json` (`"Seo": { "SiteBaseUrl": "" }`), `appsettings.Development.json` (`http://localhost:3000`).

### Hạ tầng
- **Sửa** `nginx/nginx.conf` — `location = /sitemap.xml` proxy MinIO, `Cache-Control` 1 giờ.

### Frontend
- **Tạo** `app/robots.ts` — `User-agent: *`, `Allow: /`, `Disallow: /dashboard/`, `Sitemap: …`.
- **Tạo** `__tests__/app/robots.test.ts`.

## 6. Test — viết trước, xác nhận fail

**`IntegrationTests/Jobs/SitemapTests.cs`** (Postgres Testcontainers + `FakeFileStorageService`):
1. Chỉ recipe Published; Draft / Archived / soft delete bị loại; số URL recipe = số Published.
2. Category soft delete bị loại.
3. Có 3 trang tĩnh; `loc` tuyệt đối theo `Seo:SiteBaseUrl`.
4. Mỗi `<url>` đủ `loc`, `lastmod`, `changefreq`, `priority`; XML parse được, đúng namespace.
5. Ghi key `seo/sitemap.xml`, `application/xml`; chạy 2 lần → ghi đè, không sinh object mới.
6. *(error)* Thiếu `Seo:SiteBaseUrl` → ném lỗi, không ghi file.
7. Job có `AutomaticRetry(Attempts = 2)` + `DisableConcurrentExecution`.
8. Recurring job đăng ký đúng id, cron `0 2 * * *`, UTC (fake `IRecurringJobManager`).

**`IntegrationTests/Files/MinioFileStorageServiceTests.cs`** — `SaveAsync` đúng key, ghi đè, đúng content-type (S3 thật).

**`UnitTests/Seo/SitemapXmlBuilderTests.cs`** — escape ký tự XML, định dạng ngày, priority `0.8` không phụ thuộc culture.

**Frontend** — `__tests__/app/robots.test.ts`.

## 7. Xong khi

- Toàn bộ test trên pass, `dotnet build` 0 warning, `npm run lint` pass.
- `traceability.md`: FR-JOB-003 ✅, NFR-SEO-003 ✅.
- `team-assignment.md`: `frontend/app/sitemap.ts` → `frontend/app/robots.ts`.
- `CHANGELOG.md` cập nhật.
- Đã báo A (`Program.cs`) và C (`IFileStorageService`).
