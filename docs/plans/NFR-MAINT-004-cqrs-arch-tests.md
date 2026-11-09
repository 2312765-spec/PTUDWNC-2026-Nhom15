# Kế hoạch — NFR-MAINT-004: architecture test tách CQRS + Domain chỉ dùng BCL (mảng D)

**Ngày:** 2026-11-09 · **Branch:** `test/NFR-MAINT-004-cqrs-arch-tests`
**Phạm vi:** chỉ thêm test trong `backend/tests/CulinaryBlog.ArchitectureTests`, không đổi code chính.
Liên quan: CONS-001, CONS-002.

## 1. Hiện trạng

`dotnet test backend/tests/CulinaryBlog.ArchitectureTests`: 12 pass, 1 skip, 0 fail.

| Vế của NFR-MAINT-004 | Hiện trạng |
|---|---|
| Dependency Rule (CONS-001) | ✅ 8 test `LayerDependencyTests` — thiếu "Domain chỉ dùng .NET BCL" (mới chặn EF Core + OpenTelemetry) |
| CQRS không trộn read/write | ❌ chưa có test |
| Quy ước `*Query` trong `Queries` | ⏸ skip "chưa có Query nào" — nay đã có 8 Query |

Rà tay: code hiện tại không vi phạm. `GetRecipeBySlugQueryHandler` gửi `GetRecipeDetailQuery` qua `ISender`
(Query → Query, hợp lệ).

## 2. Việc làm

| File | Thay đổi |
|---|---|
| `ConventionTests.cs` | Bỏ `Skip` test Query nằm trong `Queries` |
| `LayerDependencyTests.cs` | +1: assembly Domain chỉ tham chiếu `System.*` / `netstandard` |
| `CqrsSeparationTests.cs` (mới, quét IL bằng Mono.Cecil như `SecurityRuleTests` — thấy được state machine async ở Release) | (1) Query không implement `ICacheInvalidator`, Command không implement `ICacheable`. (2) Code trong `*.Queries.*` không dùng `IUnitOfWork`, `ICacheInvalidator`, `IFileStorageService`. (3) Không gọi method ghi của repository (`AddAsync`, `TryAddAsync`, `Update`, `Delete`, `SaveStepsAsync`, `SaveChangesAsync`, `*ForUpdateAsync`). (4) Không tạo Command. + Test đối chứng: bộ quét thấy Command handler gọi `SaveChangesAsync` và Query handler tạo `GetRecipeDetailQuery` |
| `docs/traceability.md` | NFR-MAINT-004 ⬜ → ✅ |

## 3. Kiểm chứng

Code đang đúng nên test mới pass ngay. Để chứng minh test bắt được lỗi: thêm tạm vi phạm (Query handler gọi
`SaveChangesAsync`) → test đỏ → gỡ ra.

## 4. Commit

`test(NFR-MAINT-004): architecture test tách CQRS read/write + Domain chỉ dùng BCL`
