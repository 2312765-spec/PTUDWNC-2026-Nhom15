# Plan — FR-OBS-003: Distributed Tracing & Metrics (OpenTelemetry)

> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn. Viết **trước khi code**
> (vòng lặp CLAUDE.md mục 9), branch `2312765-NVThuan-FR-OBS-003` (tách từ
> `fix/FR-OBS-002-structured-logging` — cần capturing log sink và enricher của FR-OBS-002).
> Slice S11.

---

## 1. Yêu cầu (SRS 3.7 dòng FR-OBS-003, bảng tích hợp 2.x + 8.x)

| Hạng mục | Nội dung |
|---|---|
| Traces | HTTP request (AspNetCore), thao tác database, HttpClient |
| Metrics | Request count, duration histogram, error rate + custom: số recipe **created** / **published** |
| Log ↔ trace | `Activity.TraceId` có trong structured log (bổ sung cho `CorrelationId` của FR-OBS-002) |
| Export | OTLP. Dev → Seq; prod → OTel Collector → Jaeger/Grafana Tempo. Endpoint qua `OTEL_EXPORTER_OTLP_ENDPOINT` |
| Xong khi (roadmap S11) | Một request bất kỳ trace được xuyên suốt trong Seq qua `CorrelationId` + `TraceId` |

## 2. Hiện trạng

- Package OpenTelemetry 1.18.0 (Hosting, AspNetCore, Http, OTLP) đã reference trong API — **chưa có
  code cấu hình**.
- Serilog ≥ 3.1 tự lấy `TraceId`/`SpanId` từ `Activity.Current`; `CompactJsonFormatter` xuất `@tr`/`@sp`.
  Phần "TraceId trong log" chủ yếu cần **test kiểm chứng**.
- ASP.NET Core 10 có sẵn meter `Microsoft.AspNetCore.Hosting` → `http.server.request.duration`
  (thuộc tính `http.response.status_code`, `error.type`) = đủ count + duration + error rate.
- `CreateRecipeCommandHandler` / `PublishRecipeCommandHandler` **chưa tồn tại** (C, S5/S7).

## 3. Quyết định mới (thêm vào `docs/decisions.md`)

D35 đã được A giữ cho FR-AUTH-004 → OBS-003 dùng **D36–D39**. Báo nhóm giữ chỗ số trước khi merge.

| Mã | Chủ đề | Chốt |
|---|---|---|
| **D36** | Đích export | Dev: **trace** → Seq qua OTLP **HTTP/protobuf** (`http://localhost:5341/ingest/otlp/v1/traces`); **metric không export** (Seq không ingest metrics). Prod: trace + metric → Collector qua `OTEL_EXPORTER_OTLP_ENDPOINT`. Exporter từng signal chỉ đăng ký khi có endpoint; môi trường `Testing` không export |
| **D37** | "EF Core traces" | Dùng **`Npgsql.OpenTelemetry`** (`AddNpgsql()`, stable, span theo câu SQL) thay `OpenTelemetry.Instrumentation.EntityFrameworkCore` (beta). `db.statement` đã tham số hóa, không chứa giá trị |
| **D38** | Định nghĩa error rate | Theo semantic convention: request có `error.type` (5xx / exception). **4xx không tính lỗi server.** Tính ở backend quan sát (Grafana) từ `http.server.request.duration` |
| **D39** | Metric nghiệp vụ khi handler chưa có | Tạo `RecipeMetrics` ngay (có unit test). Việc **gọi** `RecordCreated()`/`RecordPublished()` thuộc FR-RCP-003/005 (C), sau khi `SaveChangesAsync` thành công. FR-OBS-003 ở trạng thái 🟡 tới khi S5/S7 xong |

Thêm: loại `/health*` khỏi trace (probe gây nhiễu).

## 4. File sẽ tạo / sửa

| Tầng | File | Thay đổi |
|---|---|---|
| Docs | `docs/decisions.md` | Thêm D36–D39 + dòng tra nhanh |
| Domain | — | Không đụng (CONS-001) |
| Application | `Common/Observability/RecipeMetrics.cs` (mới) | `IMeterFactory` → meter `CulinaryBlog.Recipes`, counter `culinaryblog.recipes.created`, `culinaryblog.recipes.published`. Chỉ `System.Diagnostics.Metrics`, không reference OpenTelemetry |
| Application | `DependencyInjection.cs` | Đăng ký `RecipeMetrics` singleton |
| Application | `CulinaryBlog.Application.csproj` | + `Microsoft.Extensions.Diagnostics.Abstractions` |
| Application | `Recipes/Commands/OWNER.md` | Ghi chú D39 cho C |
| API | `Extensions/TelemetryExtensions.cs` (mới) | `AddAppTelemetry(configuration, environment)`: resource `service.name=culinary-blog-api`; tracing AspNetCore (filter `/health`) + HttpClient + Npgsql + source `CulinaryBlog.*`; metrics `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`, `System.Net.Http`, `CulinaryBlog.Recipes`; OTLP exporter theo D36 |
| API | `Program.cs` | 1 dòng `builder.Services.AddAppTelemetry(...)` (file dùng chung — báo nhóm) |
| API | `CulinaryBlog.API.csproj` | + `Npgsql.OpenTelemetry` |
| API | `appsettings.Development.json` | Endpoint trace → Seq, protocol `http/protobuf` |
| Root | `Directory.Packages.props` | + `Npgsql.OpenTelemetry`, `Microsoft.Extensions.Diagnostics.Abstractions`, (test) `OpenTelemetry.Exporter.InMemory`, `Microsoft.Extensions.Diagnostics.Testing` |

**Không đụng:** Infrastructure, schema/migration, `docker-compose.yml` (Seq đã mở 5341).

## 5. Test (viết trước, chạy xác nhận fail)

**Integration — `IntegrationTests/Observability/TracingTests.cs`** (InMemory exporter + capturing log
sink qua `ConfigureTestServices`, như `StructuredLoggingTests`):

1. `FR-OBS-003`: request API sinh server span đúng `http.route` + status.
2. `FR-OBS-003/D37`: request đọc DB (`GET /api/v1/categories`, Testcontainers Postgres) sinh span
   Npgsql **cùng TraceId** với server span.
3. `FR-OBS-003`: mọi log event của request có **TraceId trùng** server span, vẫn có `CorrelationId`.
4. `FR-OBS-003`: client gửi `traceparent` → span và log giữ nguyên TraceId của client.
5. `FR-OBS-003`: `/health/live` không sinh span.
6. `FR-OBS-003/D38`: request 500 ghi `http.server.request.duration` có `error.type`; 404 thì không.
7. `FR-OBS-003`: gọi `/auth/refresh` → không span nào chứa chuỗi refresh token (không lộ secret).

**Unit — `UnitTests/Observability/RecipeMetricsTests.cs`** (`MetricCollector<long>`):
`RecordCreated` / `RecordPublished` tăng counter đúng 1 mỗi lần.

**Architecture — `LayerDependencyTests.cs`**: Domain và Application không phụ thuộc namespace `OpenTelemetry`.

## 6. Kết thúc

- `docs/traceability.md`: FR-OBS-003 → 🟡 (metric nghiệp vụ chờ FR-RCP-003/005), điền file + test.
- `dotnet build` 0 warning, `dotnet test` pass.
- Kiểm tay: `docker compose up -d`, gọi API, xác nhận trace hiện trong Seq và lọc log theo TraceId.
- Commit `feat(FR-OBS-003): ...`.

## 6b. Kết quả implement (2026-09-30)

Lệch so với kế hoạch, phát hiện lúc code:

- **`GlobalExceptionMiddleware` phải sửa** (không có trong mục 4): middleware nuốt exception nên
  ASP.NET Core Hosting không gắn `error.type` cho 500 — thêm tag qua `IHttpMetricsTagsFeature`,
  như `ExceptionHandlerMiddleware` có sẵn làm (D38).
- **`AddOtlpExporter()` bỏ qua biến riêng từng signal** (`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT/PROTOCOL`)
  → rơi về gRPC `localhost:4317`, span không tới Seq dù log có TraceId. Sửa: áp endpoint/protocol
  tường minh (`ApplySignalOverrides`). Kiểm tay xác nhận span Server + Npgsql vào Seq (2026.1).
- Test số 7 dùng `POST /auth/login` (mật khẩu) thay `/auth/refresh` — FR-AUTH-004 chưa có.

## 7. Rủi ro

- "Seq không ingest metrics / chỉ OTLP HTTP" dựa trên hiểu biết về Seq 2024+, **chưa kiểm** trên
  image `datalust/seq:latest` — kiểm bằng cách gửi trace thật ở bước Code; sai thì sửa D36.
- Chưa có `docker-compose.prod.yml` → Collector/Tempo prod chỉ dừng ở biến môi trường.
- Số D36–D39 có thể va với quyết định mới của người khác — giữ chỗ trong nhóm, rebase khi merge.
