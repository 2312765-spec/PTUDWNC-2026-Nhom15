# Ma trận Phân quyền — Culinary Blog v1.0.0

Nguồn: SRS mục 2.3 (Các lớp Người dùng), NFR-SEC-006, và cột "Auth / Role" của Chương 8,
đã áp dụng các quyết định **D5, D11, D12, D16, D17** trong `decisions.md`.

---

## 1. Hai tầng phân quyền

| Tầng | Cơ chế | Câu hỏi nó trả lời | Hiện thực |
|---|---|---|---|
| 1 | **Role-Based** | "Anh là ai?" — Guest / Author / Admin | `RequireAuthorization("AuthorPolicy" \| "AdminPolicy")` |
| 2 | **Resource-Based** | "Đây có phải đồ của anh không?" — `AuthorId == currentUserId` | `IAuthorizationService.AuthorizeAsync(user, recipe, Operations.Update)` trong **Application Layer** |

> **SRS mục 2.3 nói có ba tầng**, tầng thứ ba là policy `VerifiedAuthor` (yêu cầu email
> đã xác nhận). **Đã bỏ khỏi v1.0.0 theo D12** — không endpoint nào trong Chương 8 dùng
> nó, và không có FR nào cho luồng xác nhận email. Hệ thống chỉ đăng ký **2 policy**:
> `AuthorPolicy` và `AdminPolicy`.

**Quy tắc cứng (NFR-SEC-006):**

- Kiểm tra quyền **ở Application Layer**, không chỉ ở endpoint. Endpoint chặn tầng 1,
  handler chặn tầng 2–3.
- **Không hardcode chuỗi role** (`if (user.IsInRole("Admin"))`). Dùng policy đã đăng ký.
- Endpoint nhạy cảm (DELETE, PATCH publish): **double-check userId ngay trước khi commit**.
- **Audit trail:** log mọi write operation kèm `userId` + `timestamp` (Serilog).
- **Admin bypass** được resource-ownership check ở mọi nơi.

---

## 2. Ba vai trò

| Vai trò | Điều kiện | Cách được gán | Ưu tiên phục vụ |
|---|---|---|---|
| **Guest** | Không cần tài khoản | Mặc định | Cao (đại đa số người dùng) |
| **Author** | Có tài khoản + JWT hợp lệ | **Tự động gán khi đăng ký** (FR-AUTH-001 bước 7) | Cao |
| **Admin** | Có tài khoản + role Admin | **Gán thủ công qua database seeding** — không có endpoint tự phong | Trung bình (số lượng ít) |

> Không có vai trò "user thường" tách khỏi Author. Mọi người đăng ký đều là Author.

---

## 3. Ma trận Tài nguyên × Hành động

Ký hiệu: `✅` cho phép · `❌` từ chối · `👤` chỉ với tài nguyên của chính mình ·
`🔒` cần đăng nhập

### 3.1 Recipe (Công thức)

| Hành động | Endpoint | Guest | Author | Author (của người khác) | Admin | Từ chối trả về |
|---|---|:--:|:--:|:--:|:--:|---|
| Xem danh sách **Published** | `GET /recipes` | ✅ | ✅ | ✅ | ✅ | — |
| Xem **Draft/Archived** trong danh sách | `GET /recipes` | ❌ | 👤 | ❌ | ✅ | Lọc khỏi kết quả (không 403) |
| Xem chi tiết **Published** | `GET /recipes/{slug}` | ✅ | ✅ | ✅ | ✅ | — |
| Xem chi tiết **Draft/Archived** | `GET /recipes/{slug}` | ❌ | 👤 | ❌ | ✅ | 403 `RECIPE_FORBIDDEN` |
| Tìm kiếm (chỉ Published) | `GET /recipes/search` | ✅ | ✅ | ✅ | ✅ | — |
| Tạo mới | `POST /recipes` | ❌ | ✅ | — | ✅ | 401 (chưa login) / 403 |
| Cập nhật | `PUT /recipes/{id}` | ❌ | 👤 | ❌ | ✅ | 403 `RECIPE_FORBIDDEN` |
| Publish / Unpublish | `PATCH /recipes/{id}/publish\|unpublish` | ❌ | 👤 | ❌ | ✅ | 403 |
| Archive | `PATCH /recipes/{id}/archive` | ❌ | 👤 | ❌ | ✅ | 403 |
| Xóa | `DELETE /recipes/{id}` | ❌ | 👤 | ❌ | ✅ | 403 |

**Authorization filter trong `GetRecipesQuery` (FR-RCP-001 bước 4):**

```
Guest   → WHERE Status = Published
Author  → WHERE Status = Published OR (Status IN (Draft, Archived) AND AuthorId = currentUserId)
Admin   → không filter
```

> Lưu ý quan trọng: danh sách **lọc** chứ không **403**. Recipe Draft của người khác
> đơn giản là không xuất hiện. Nhưng truy cập **trực tiếp** theo slug thì trả 403
> (SRS FR-RCP-002 A2) — chấp nhận rằng điều này để lộ "slug này có tồn tại".
> Nếu muốn kín hoàn toàn thì phải trả 404; SRS chọn 403. Đây là quyết định có chủ đích.

### 3.2 Recipe con — Steps / Ingredients / Images

Tất cả thừa kế quyền của Recipe cha. Không có ngoại lệ.

| Hành động | Endpoint | Guest | Author (owner) | Author (khác) | Admin |
|---|---|:--:|:--:|:--:|:--:|
| Xem (nhúng trong recipe detail) | `GET /recipes/{slug}` | ✅ nếu recipe Published | 👤 | ✅ nếu Published | ✅ |
| Thêm/sửa/xóa bước | `POST/PUT/DELETE /recipes/{id}/steps/{stepId?}` | ❌ | 👤 | ❌ | ✅ |
| Thêm/sửa/xóa nguyên liệu | `POST/PUT/DELETE /recipes/{id}/ingredients/{ingId?}` | ❌ | 👤 | ❌ | ✅ |
| Upload ảnh | `POST /recipes/{id}/images` | ❌ | 👤 | ❌ | ✅ |
| Đặt ảnh chính / sửa metadata | `PATCH /recipes/{id}/images/{imageId}` | ❌ | 👤 | ❌ | ✅ |
| Xóa ảnh | `DELETE /recipes/{id}/images/{imageId}` | ❌ | 👤 | ❌ | ✅ |

> **Rủi ro đã chấp nhận (D16):** bucket MinIO đặt policy `public-read`, nên ảnh của recipe
> **Draft** vẫn truy cập được bằng URL trực tiếp nếu ai đó biết URL. URL chứa GUID v4
> (`recipes/{recipeId}/{guid}.jpg`) nên không đoán được trong thực tế. Không lộ dữ liệu
> văn bản của recipe. Đã ghi vào `docs/adr/0001-minio-public-read.md`.

### 3.3 Category (Danh mục)

| Hành động | Endpoint | Guest | Author | Admin |
|---|---|:--:|:--:|:--:|
| Xem danh sách | `GET /categories` | ✅ | ✅ | ✅ |
| Xem chi tiết + recipes | `GET /categories/{slug}` | ✅ | ✅ | ✅ |
| Tạo | `POST /categories` | ❌ 401 | ❌ 403 | ✅ |
| Cập nhật | `PUT /categories/{id}` | ❌ 401 | ❌ 403 | ✅ |
| Xóa | `DELETE /categories/{id}` | ❌ 401 | ❌ 403 | ✅ |

**Ràng buộc nghiệp vụ:** không xóa được category còn recipe (kể cả Draft) →
409 `CATEGORY_DELETE_HAS_RECIPES`. Admin phải chuyển recipe sang category khác trước.
FK `Recipes.CategoryId` đặt `ON DELETE RESTRICT`.

### 3.4 Tài khoản & Hồ sơ

| Hành động | Endpoint | Guest | Author | Admin |
|---|---|:--:|:--:|:--:|
| Đăng ký | `POST /auth/register` | ✅ | — | — |
| Đăng nhập | `POST /auth/login` | ✅ | — | — |
| Google OAuth | `POST /auth/google` | ✅ | — | — |
| Refresh token | `POST /auth/refresh` | 🔒 có RT hợp lệ | ✅ | ✅ |
| Đăng xuất | `POST /auth/logout` | ❌ | ✅ | ✅ |
| Xem hồ sơ **của mình** | `GET /auth/me` | ❌ 401 | 👤 | 👤 |
| Sửa hồ sơ **của mình** | `PATCH /auth/me` | ❌ 401 | 👤 | 👤 |
| Xem/sửa hồ sơ người khác | — | ❌ | ❌ | ❌ ngoài scope v1 |
| Ban / deactivate user | — | ❌ | ❌ | ❌ ngoài scope v1 — sửa trực tiếp DB |

> **D11 — Admin quản lý user nằm ngoài scope v1.0.0.** Cột `IsActive` vẫn tồn tại và
> **vẫn được kiểm tra khi đăng nhập và refresh token** (`IsActive == false` → 403
> `AUTH_ACCOUNT_DISABLED`), nhưng không có endpoint nào để bật/tắt nó. Cần ban ai thì
> Admin chạy `UPDATE "AspNetUsers" SET "IsActive" = false WHERE "Email" = '...'`.
>
> **D5 —** `PATCH /auth/me` chỉ cho sửa `displayName`, `avatarUrl`, `bio`. Email và
> username không đổi được qua endpoint này (cần quy trình xác nhận riêng, ngoài scope).
> Không tồn tại trường `fullName` hay `userName` trong bất kỳ request/response nào.

### 3.5 Hạ tầng & Vận hành

| Tài nguyên | Đường dẫn | Guest | Author | Admin |
|---|---|:--:|:--:|:--:|
| Health check tổng hợp | `GET /health` | ✅ | ✅ | ✅ |
| Liveness / Readiness probe | `GET /health/live` `/health/ready` | ✅ | ✅ | ✅ |
| API docs (Scalar UI) | `/scalar` | ✅ | ✅ | ✅ |
| **Hangfire Dashboard** | `/hangfire` | ❌ | ❌ | ✅ (policy-protected) |
| Structured logs (Seq) | dev only | ❌ | ❌ | ✅ |

---

## 4. Giao diện Frontend — quyền theo route

Từ SRS mục 5.1. Middleware Next.js chặn ở tầng route, backend vẫn phải chặn lại.

| Route | Rendering | Yêu cầu | Redirect nếu thiếu quyền |
|---|---|---|---|
| `/` | ISR 3600 | Không | — |
| `/recipes` | SSR | Không | — |
| `/recipes/[slug]` | ISR 300 | Không (Draft: owner/Admin) | `/404` hoặc `/403` |
| `/categories` | ISR 3600 | Không | — |
| `/categories/[slug]` | ISR 600 | Không | — |
| `/search` | SSR | Không | — |
| `/auth/login` | CSR | Không (redirect nếu đã login) | → `/dashboard` |
| `/auth/register` | CSR | Không | — |
| `/dashboard` | CSR | Author/Admin | → `/auth/login?callbackUrl=` |
| `/dashboard/recipes` | CSR | Author/Admin | → `/auth/login` |
| `/dashboard/recipes/new` | CSR | Author/Admin | → `/auth/login` |
| `/dashboard/recipes/[id]/edit` | CSR | **Owner** hoặc Admin | → `/403` |
| `/dashboard/categories` | CSR | **Admin** | → `/403` |
| `/profile` | CSR | Đăng nhập | → `/auth/login` |

> **Không tin frontend.** Middleware Next.js chỉ để UX. Mọi kiểm tra quyền thật
> nằm ở backend Application Layer.

---

## 5. Mã lỗi phân quyền

| Tình huống | HTTP | Application Error Code |
|---|---|---|
| Không gửi token / token sai định dạng | 401 | `AUTH_TOKEN_INVALID` |
| Access token hết hạn (15 phút) | 401 | `AUTH_TOKEN_EXPIRED` |
| Refresh token hết hạn (7 ngày) | 401 | `AUTH_REFRESH_TOKEN_EXPIRED` |
| Refresh token đã revoke, bị dùng lại | 401 | `AUTH_REFRESH_TOKEN_REVOKED` + **LOG WARNING** + revoke cả token family |
| Sai email/mật khẩu | 401 | `AUTH_INVALID_CREDENTIALS` (message generic — chống User Enumeration) |
| Tài khoản bị khóa do sai 5 lần (15 phút) | **423** | `AUTH_ACCOUNT_LOCKED` (thêm mới theo D17) |
| Tài khoản bị vô hiệu hóa (`IsActive = false`) | 403 | `AUTH_ACCOUNT_DISABLED` — kiểm tra ở cả login và refresh (D11) |
| Đã login, thiếu role Admin | 403 | — (dùng RFC 7807 chung) |
| Đã login, không phải owner của recipe | 403 | `RECIPE_FORBIDDEN` |
| Vượt rate limit | 429 | `RATE_LIMIT_EXCEEDED` + header `Retry-After` |

---

## 6. Bộ test phân quyền tối thiểu

Mỗi endpoint có bảo vệ cần ít nhất 4 test:

```csharp
[Fact] // 1. Không token
public async Task Endpoint_WithoutToken_Returns401()

[Fact] // 2. Sai role
public async Task Endpoint_AsAuthor_OnAdminEndpoint_Returns403()

[Fact] // 3. Đúng role nhưng không phải owner
public async Task Endpoint_AsOtherAuthor_Returns403()

[Fact] // 4. Admin bypass được ownership
public async Task Endpoint_AsAdmin_OnOthersResource_Returns200()
```

**Ba test dễ quên nhất — đừng bỏ:**

1. Danh sách recipe của Guest **không chứa** Draft của bất kỳ ai.
2. Author A **không thấy** Draft của Author B trong danh sách, và nhận 403 khi truy cập
   trực tiếp theo slug.
3. Admin xóa/sửa được recipe của Author khác (bypass ownership).
