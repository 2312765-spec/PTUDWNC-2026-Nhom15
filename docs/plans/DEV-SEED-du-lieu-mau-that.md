# Plan — DEV-SEED: Dữ liệu mẫu Development có nghĩa

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-10-14.
> Branch: `chore/dev-seed-real-data` (tách từ `main`).

---

## 1. Phạm vi và truy vết

Không gắn với một FR cụ thể — đây là dữ liệu mẫu cho môi trường Development, truy về:

| Nguồn | Yêu cầu |
|---|---|
| SRS dòng 295 | Dữ liệu seed tạo bằng **Bogus**, **50 recipe** + **5 tác giả** |
| Roadmap S4 · team-assignment (B) | Seed **cả Draft và Archived** để bug phân quyền lộ sớm |
| Roadmap mục Auth | Seed role `Author`, `Admin` + **1 tài khoản Admin** |
| SRS dòng 226 | Admin "được gán thủ công qua database seeding" |

Giữ nguyên Bogus và con số 50/5 → **không mâu thuẫn SRS, không cần D-x mới**.

**Ngoài phạm vi:** đổi entity, migration mới, seed cho Production (D29 đã ghi đây là việc riêng).

## 2. Sự cố dẫn tới kế hoạch này (2026-10-14)

Seeder Sprint 0 dùng `faker.Lorem` / `faker.Commerce` → DB dev chứa tên món "Handcrafted Wooden
Mouse", nguyên liệu "Granite", "Rubber", mô tả là chuỗi từ tiếng Việt ghép ngẫu nhiên vô nghĩa;
5 user seed không có role `Author`. Commit `4b78dfb` đã đổi seeder thành no-op nhưng dữ liệu cũ
còn trong DB. Đã dọn ngày 2026-10-14 (backup `D:\culinary-db-backups\backup-culinaryblog-2026-10-14.dump`):
xóa 5 user + 50 recipe seed, giữ 8 category, user thật và recipe test.

## 3. Thiết kế

| Lỗi cũ | Cách mới |
|---|---|
| Nội dung từ `Lorem`/`Commerce` | Catalog viết tay 50 món Việt thật — mô tả, 3–5 bước, 4–8 nguyên liệu có định lượng thực tế. Bogus chỉ dùng để **chọn ngẫu nhiên** (tác giả, prep/cook time, servings, difficulty) |
| Idempotent theo `Users.Any()` | Idempotent theo **email seed cố định** (`*@seed.culinaryblog.local`) |
| User không có role | Tạo qua `UserManager.CreateAsync(user, password)` + `AddToRoleAsync` |
| Tự tạo category | Tìm 8 category theo slug, chỉ tạo khi thiếu |
| `status: Published` gán thẳng | `recipe.Publish()` (kiểm D3) → `Archive()` cho Archived; trạng thái rải đều `i % 3` |
| Không tất định | `new Faker("vi") { Random = new Randomizer(<seed cố định>) }` — không đụng `Randomizer.Seed` toàn cục |

Tài khoản seed (chỉ Development, mật khẩu dev `Password123!` — không dùng ngoài Development):

- 5 Author: `author1..5@seed.culinaryblog.local`, DisplayName/Bio tiếng Việt có nghĩa.
- 1 Admin: `admin@seed.culinaryblog.local`, role `Admin`.

Slug sinh bằng `ISlugHelper.Generate` + suffix cục bộ `-2`, `-3` (D10). Ảnh: `placehold.co` với
text = slug (ASCII), AltText = tên món.

## 4. File tạo / sửa

| Tầng | File | Việc |
|---|---|---|
| Infrastructure | `Persistence/Seeding/SeedRecipeCatalog.cs` *(mới)* | Dữ liệu tĩnh: 5 tác giả, 8 category, 50 món |
| Infrastructure | `Persistence/Seeding/DbSeeder.cs` | Viết lại theo mục 3 |
| API | `Program.cs` | Truyền `UserManager<ApplicationUser>`, `ISlugHelper` vào `DbSeeder.SeedAsync`; vẫn chỉ `IsDevelopment()` |
| Tests | `IntegrationTests/Seeding/DbSeederTests.cs` *(mới)* | Xem mục 5 |
| Docs | `docs/traceability.md` | Thêm dòng seed dev → SRS 295 / roadmap S4 |

## 5. Test (viết trước, xác nhận fail)

Testcontainers Postgres (`PostgresApiFactory`):

1. Tạo đúng 5 Author (có role `Author`) + 1 Admin (có role `Admin`); đăng nhập được bằng mật khẩu dev.
2. 50 recipe, có đủ Draft / Published / Archived.
3. Mọi recipe Published/Archived có ≥ 1 step và ≥ 1 ingredient (D3).
4. Gọi `SeedAsync` 2 lần → số lượng không đổi.
5. Đã có 8 category → không tạo thêm category.
6. User / recipe có sẵn không bị đụng tới.
7. Mọi tên món thuộc `SeedRecipeCatalog` — không còn chữ sinh ngẫu nhiên.

## 6. Quy trình

1. Viết test → chạy → xác nhận fail.
2. Code đến khi pass, `dotnet build` 0 warning.
3. Chạy API Development → seed vào DB dev → kiểm tra bằng SQL.
4. Cập nhật `traceability.md`, commit `chore(seed): ...`.
