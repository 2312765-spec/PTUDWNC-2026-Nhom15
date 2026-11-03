# Changelog

Theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/) và [SemVer](https://semver.org/lang/vi/)
(NFR-MAINT-003).

## [Unreleased]

### Added
- Khung dự án Sprint 0: solution 4 tầng Clean Architecture + 3 project test,
  MediatR pipeline (4 behavior), GlobalExceptionMiddleware theo RFC 7807,
  CorrelationIdMiddleware, health check 3 endpoint, cache Redis tag-based.
- 30 endpoint khai báo sẵn (trả 501) để Scalar UI hiển thị đầy đủ API contract từ ngày đầu.
- Frontend Next.js 16 App Router + Tailwind 4 + TanStack Query + design token.
- Hạ tầng dev: docker-compose (postgres, redis, minio, seq, mailhog), Nginx, GitHub Actions CI.
- Architecture test kiểm tra Dependency Rule (CONS-001).
- Tài liệu: `docs/decisions.md` (22 quyết định chốt mâu thuẫn SRS), `traceability.md`,
  `permissions.md`, `roadmap.md`, `team-assignment.md`.
- FR-JOB-003 / NFR-SEO-003: Hangfire recurring `SitemapGenerationJob` (02:00 UTC, retry 2 lần,
  `DisableConcurrentExecution`) sinh `sitemap.xml` (trang tĩnh + category + recipe Published) lên MinIO
  `seo/sitemap.xml`; Nginx phục vụ `/sitemap.xml`; `frontend/app/robots.ts`. Cấu hình mới `Seo:SiteBaseUrl`.
  `IFileStorageService.SaveAsync` cho file hệ thống sinh. Không ping Google (D51–D53).
- CONS-009: `docker-compose.prod.yml` — chỉ Nginx publish 80/443 (D60), HTTPS TLS 1.2+ với HTTP → 301 và
  HSTS 1 năm (`nginx/nginx.prod.conf`, NFR-SEC-005), service `migrate` chạy `--migrate-only` trước API (D59),
  ảnh MinIO phục vụ qua Nginx `/culinary-blog/` với cấu hình mới `Minio:PublicBaseUrl` (D58), sao lưu
  `pg_dump` 03:00 UTC giữ 30 ngày (`scripts/pg-backup.sh`, NFR-REL-003), smoke test `scripts/verify-prod.sh`.
  Hướng dẫn: `docs/deployment.md`. API bật `UseForwardedHeaders`; frontend build `output: 'standalone'`.

### Security
- NFR-SEC-007: bỏ `Jwt:Key` khỏi `appsettings.json` và khóa dự phòng hardcode trong `Program.cs`. API dừng
  ngay khi thiếu khóa hoặc khóa < 32 byte. **Khóa cũ vẫn còn trong lịch sử Git — production phải dùng khóa mới.**
