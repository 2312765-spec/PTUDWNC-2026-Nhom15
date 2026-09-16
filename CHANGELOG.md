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
