# Kế hoạch — NFR-MAINT-002: test coverage

**Ngày:** 2026-11-10 · **Branch:** `test/NFR-MAINT-002-coverage`
**Phạm vi:** test + CI. Không đổi code chạy thật, không có FR mới. Quyết định áp dụng: D4, D8, D90.

SRS NFR-MAINT-002: (1) unit test ≥ 80% line coverage tầng Application (commands, queries, validators);
(2) mọi API endpoint có ≥ 1 happy path + 1 error case; (3) E2E 5 luồng: register, login, create recipe,
publish, search.

## 1. Hiện trạng (đo 2026-11-10 trên `main` b2765b7)

| Vế | Kết quả | Đạt |
|---|---|---|
| Unit — tầng Application | **86,3%** (1.832/2.124 dòng, coverlet, dedup theo dòng) | ✅ |
| E2E 5 luồng | `auth.spec` (register, login) · `recipe-editor.spec` (tạo, xuất bản) · `search.spec` | ✅ |
| Endpoint happy + error | 31/34. Thiếu error case: `GET /categories`, `GET /health`. `GET /health/live` không có error case theo thiết kế (FR-OBS-001: luôn Healthy) | 🟡 |
| CI | Thu coverage (`--collect:"XPlat Code Coverage"`) nhưng **không kiểm ngưỡng** | ❌ |

File Application 0% unit coverage (chỉ có integration test phủ gián tiếp): `ValidationBehavior`,
`CachingBehavior`, `CacheInvalidationBehavior`, `UpdateRecipeImageCommandHandler`, `GetRecipeByIdQueryHandler`,
`GetCategoriesQueryHandler`.

## 2. Việc làm

| # | Việc | File |
|---|---|---|
| 1 | Script đọc cobertura, tính line coverage tầng Application (bỏ code sinh tự động trong `obj/`), < 80% → exit 1 | `scripts/check-coverage.py` (mới) |
| 2 | CI chạy script ngay sau bước Unit tests | `.github/workflows/ci.yml` |
| 3 | Integration: DB chết → `GET /api/v1/categories` 500 `application/problem+json`, không lộ stack trace | `IntegrationTests/Observability/DatabaseDownTests.cs` (mới, dùng `DegradedApiFactory`) |
| 4 | Integration: DB chết → `GET /health` 503 | như trên |
| 5 | Unit `ValidationBehavior`: không có validator → gọi handler; validator lỗi → `ValidationException`, KHÔNG gọi handler; nhiều validator gộp lỗi | `UnitTests/Common/Behaviors/ValidationBehaviorTests.cs` |
| 6 | Unit `CachingBehavior`: request không `ICacheable` → bỏ qua cache; hit → không gọi handler; miss → gọi handler + ghi cache đúng key/TTL/tag; GET ném lỗi → vẫn gọi handler; SET ném lỗi → vẫn trả response | `UnitTests/Common/Behaviors/CachingBehaviorTests.cs` |
| 7 | Unit `CacheInvalidationBehavior`: xóa tag SAU handler; handler ném lỗi → không xóa; tag rỗng / không `ICacheInvalidator` → không gọi cache; xóa ném lỗi → vẫn trả response | `UnitTests/Common/Behaviors/CacheInvalidationBehaviorTests.cs` |
| 8 | Ghi kết quả, bảng endpoint → test | `docs/traceability.md` (NFR-MAINT-002) |

Không làm: unit test `UpdateRecipeImageCommandHandler` (D), `GetRecipeByIdQueryHandler` (C) — mảng người khác,
đã có integration test; `GetCategoriesQueryHandler` — đang sửa ở nhánh NFR-PERF-004.

## 3. Test trước

Mục 3–4: chạy, xác nhận kết quả (endpoint đã đúng thì test xanh ngay — đây là test bổ sung bằng chứng, không
phải sửa lỗi; nếu đỏ thì là lỗi thật). Mục 5–7: test hành vi của code đã có — tương tự. Mục 1: chạy với ngưỡng
giả 99% → phải fail, 80% → pass.

## 4. Kết quả (2026-11-10)

### Unit coverage

| | Trước | Sau |
|---|---|---|
| Tầng Application | 86,3% (1.832/2.124) | **89,4%** (1.847/2.067) |

Số dòng tổng giảm vì script bỏ code sinh tự động trong `obj/` (`[GeneratedRegex]`). Cả 3 behavior từ 0% lên được
phủ. Còn 0%: `UpdateRecipeImageCommandHandler` (D), `GetRecipeByIdQueryHandler` (C), `GetCategoriesQueryHandler`
(đang sửa ở nhánh NFR-PERF-004), `GoogleLoginCommandValidator`, `DependencyInjection.cs` — đều có integration test.

**Lỗi thật tìm ra:** `ValidationBehavior` dùng chung một `ValidationContext` cho mọi validator và chạy song song
(`Task.WhenAll`) → mỗi lỗi bị báo 2 lần khi có ≥ 2 validator; validator có rule async đọc DB sẽ đụng cùng
`DbContext`. Chưa ảnh hưởng request nào (mỗi command hiện chỉ có 1 validator). Sửa: mỗi validator một context,
chạy tuần tự. Test: `MultipleValidators_AggregatesAllFailures` (đỏ trước khi sửa).

### Endpoint → test (integration)

Rà tay theo `DisplayName` và assertion (bộ quét regex bỏ sót các test gọi qua helper `RecipeCommandClient`).

**Lỗ hổng tìm ra — FR-CAT-005:** `DeleteCategoryTests` cũ chạy trên `CulinaryBlogApiFactory` (không có DB), không
gửi token; test "ID không tồn tại" luôn nhận 401 nhưng assert `BeOneOf(404, 401, 403)` nên luôn xanh. Không có
integration test nào cho xóa thành công (204) và D2 (còn recipe → 409). Viết lại trên `PostgresApiFactory`: 204 +
soft delete thật trong DB + cache bị xóa, 409 với recipe Draft/Published/Archived, recipe đã xóa mềm không tính,
404, xóa hai lần → 404, Author → 403, Guest → 401.

| Endpoint | Happy | Error | File test |
|---|---|---|---|
| `POST /auth/register` | 201 | 400, 409 | `Auth/RegisterTests.cs` |
| `POST /auth/login` | 200 | 400, 401, 423 | `Auth/LoginTests.cs` |
| `POST /auth/google` | 200 | 400, 502 | `Auth/GoogleLoginTests.cs` |
| `POST /auth/refresh` | 200 | 400, 401, 403 | `Auth/RefreshTokenTests.cs` |
| `POST /auth/logout` | 204 | 401 | `Auth/LogoutTests.cs` |
| `GET /auth/me` | 200 | 401 | `Auth/ProfileTests.cs` |
| `PATCH /auth/me` | 200 | 400, 401, 404 | `Auth/UpdateProfileTests.cs` |
| `GET /categories` | 200 | **500 khi DB chết (mới)** | `Categories/GetCategoriesTests.cs`, `Observability/DatabaseDownTests.cs` |
| `GET /categories/{slug}` | 200 | 404 | `Categories/GetCategoryBySlugTests.cs` |
| `POST /categories` | 201 | 400, 401, 403, 409 | `Categories/CreateCategoryTests.cs` |
| `PUT /categories/{id}` | 200 | 403, 404, 409 | `Categories/UpdateCategoryTests.cs` |
| `DELETE /categories/{id}` | 204 **(mới)** | 401, 403, 404, 409 **(mới)** | `Categories/DeleteCategoryTests.cs` (viết lại) |
| `GET /recipes` | 200 | 400, 401 | `Recipes/GetRecipesTests.cs` |
| `GET /recipes/search` | 200 | 400 | `Recipes/SearchRecipesTests.cs` |
| `GET /recipes/{id}` | 200 | 403, 404 | `Recipes/GetByIdTests.cs` |
| `GET /recipes/{slug}` | 200 | 403, 404 | `Recipes/DetailTests.cs` |
| `POST /recipes` | 201 | 400, 401 | `Recipes/CreateTests.cs` |
| `PUT /recipes/{id}` | 200 | 400, 401, 403, 404, 409 | `Recipes/UpdateTests.cs` |
| `PATCH /recipes/{id}/publish` | 200 | 400, 401, 403, 404, 409 | `Recipes/PublishTests.cs` |
| `PATCH /recipes/{id}/unpublish` | 200 | 403 | `Recipes/PublishTests.cs` |
| `PATCH /recipes/{id}/archive` | 200 | 400, 401, 403, 404, 409 | `Recipes/ArchiveTests.cs` |
| `DELETE /recipes/{id}` | 204 | 403, 404 | `Recipes/DeleteTests.cs` |
| `POST/PUT/DELETE /recipes/{id}/steps…` | 201 / 200 / 204 | 400, 401, 403, 404 (cả 3 verb) | `Recipes/StepsTests.cs` |
| `POST/PUT/DELETE /recipes/{id}/ingredients…` | 201 / 200 / 204 | 400, 401, 403, 404 (cả 3 verb) | `Recipes/IngredientsTests.cs` |
| `POST /recipes/{id}/images` | 201 | 400, 413 | `Recipes/ImagesTests.cs` |
| `PATCH /recipes/{id}/images/{imageId}` | 200 | 400, 403, 404 | `Recipes/ImagesTests.cs` |
| `DELETE /recipes/{id}/images/{imageId}` | 204 | 403, 404 | `Recipes/ImagesTests.cs` |
| `GET /health` | 200 | **503 khi DB chết (mới)** | `Observability/HealthTests.cs`, `Observability/DatabaseDownTests.cs` |
| `GET /health/live` | 200 | — không có, theo thiết kế (FR-OBS-001: luôn Healthy) | `Observability/HealthTests.cs` |
| `GET /health/ready` | 200 | 503 | `Observability/HealthTests.cs` |

### E2E 5 luồng

register, login → `e2e/auth.spec.ts` · create recipe, publish → `e2e/recipe-editor.spec.ts` · search →
`e2e/search.spec.ts`.
