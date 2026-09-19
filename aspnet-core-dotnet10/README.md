# Culinary Blog - Category Management Module (ASP.NET Core .NET 10)
> **Tài liệu tham chiếu:** Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.0 (CONFIDENTIAL • Trang 24-26 / 71)

Dự án này triển khai toàn bộ **Module Quản lý Danh mục (FR-CAT)** phân loại công thức nấu ăn bằng **ASP.NET Core (.NET 10)** theo đúng chuẩn kiến trúc **Clean Architecture + CQRS với MediatR**, tích hợp bộ nhớ đệm **IMemoryCache (Sliding Expiration 60 phút)**, chuẩn hóa ngoại lệ **RFC 7807 Problem Details** và phân quyền phân chia vai trò (Guest / Author / Admin).

---

## 1. Cấu Trúc Giải Pháp (.NET 10 Solution)

```
aspnet-core-dotnet10/
├── CulinaryBlog.sln
├── src/
│   ├── CulinaryBlog.Domain/                     # Enterprise Business Rules & Entities
│   │   ├── Entities/
│   │   │   ├── Category.cs                      # Category Entity + Factory Category.Create(...)
│   │   │   └── Recipe.cs                        # Recipe Entity (Draft / Published / Archived)
│   │   ├── Enums/
│   │   │   └── RecipeStatus.cs
│   │   ├── Interfaces/
│   │   │   ├── ICategoryRepository.cs           # Repository abstraction
│   │   │   └── IUnitOfWork.cs                   # IUnitOfWork.Categories
│   │   └── CulinaryBlog.Domain.csproj
│   │
│   ├── CulinaryBlog.Application/                # Application Business Rules (CQRS / MediatR)
│   │   ├── Categories/
│   │   │   ├── DTOs/
│   │   │   │   └── CategoryDto.cs               # CategoryDto, RecipeSummaryDto, PagedResult<T>
│   │   │   ├── Queries/
│   │   │   │   ├── GetCategories/               # FR-CAT-001: IMemoryCache (TTL 60 min sliding)
│   │   │   │   └── GetCategoryBySlug/           # FR-CAT-002: Slug lookup + OFFSET pagination
│   │   │   └── Commands/
│   │   │       └── CreateCategory/              # FR-CAT-003: Admin only, slugify + cache eviction
│   │   ├── Common/
│   │   │   ├── Behaviors/ValidationBehavior.cs  # FluentValidation pipeline
│   │   │   ├── Exceptions/CustomExceptions.cs   # NotFound, Conflict, Validation, Forbidden
│   │   │   └── Helpers/SlugHelper.cs            # Unaccent tiếng Việt, slugify + suffix unique
│   │   └── CulinaryBlog.Application.csproj
│   │
│   ├── CulinaryBlog.Infrastructure/             # Frameworks & Drivers (EF Core / Persistence)
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs          # EF Core DbContext & Entity Configuration
│   │   │   ├── Repositories/CategoryRepository.cs
│   │   │   └── UnitOfWork.cs
│   │   └── CulinaryBlog.Infrastructure.csproj
│   │
│   └── CulinaryBlog.WebApi/                     # Presentation Layer (REST API Endpoints)
│       ├── Controllers/CategoriesController.cs  # RESTful API Endpoints
│       ├── Middleware/ProblemDetailsExceptionMiddleware.cs # RFC 7807 Error Handling
│       ├── Services/CurrentUserService.cs       # JWT Claims & Role extraction
│       ├── Program.cs                           # .NET 10 DI, Middleware, Seeder & Swagger
│       ├── appsettings.json
│       └── CulinaryBlog.WebApi.csproj
```

---

## 2. Ánh Xạ Đặc Tả Yêu Cầu Chức Năng (SRS)

| Mã Yêu Cầu | Tên Yêu Cầu | HTTP Method & Endpoint | Kỹ Thuật & Luồng Xử Lý Chính |
| :--- | :--- | :--- | :--- |
| **FR-CAT-001** | Xem Danh Sách Tất Cả Danh Mục | `GET /api/v1/categories` | • Dispatch `GetCategoriesQuery` qua MediatR.<br>• Kiểm tra `IMemoryCache` key `"categories:all"`.<br>• Cache Hit: Trả ngay dữ liệu.<br>• Cache Miss: Query `IUnitOfWork.Categories.GetAllWithRecipeCount()`, sắp xếp `Name` A-Z, lưu vào `IMemoryCache` với **TTL 60 phút (sliding expiration)**.<br>• Status: `200 OK` với `CategoryDto[]`. |
| **FR-CAT-002** | Xem Chi Tiết Danh Mục & Công Thức | `GET /api/v1/categories/{slug}?page=1&pageSize=12` | • Dispatch `GetCategoryBySlugQuery`.<br>• Tìm category theo slug; nếu không tồn tại trả về `404 Not Found` (RFC 7807 Problem Details).<br>• Phân quyền: **Guest** chỉ thấy `Published`; **Author** thấy `Published` + `Draft` của chính mình; **Admin** thấy tất cả.<br>• Phân trang OFFSET-based: `SKIP (page-1)*pageSize TAKE pageSize`.<br>• Status: `200 OK` với `CategoryDetailDto`. |
| **FR-CAT-003** | Tạo Danh Mục Mới [Admin] | `POST /api/v1/categories` | • Yêu cầu `Authorization: Bearer {adminJwt}` & `[Authorize(Roles = "Admin")]`. Không có quyền trả `403 Forbidden`.<br>• `ValidationBehavior`: Name 2–50 ký tự, không chứa HTML (lỗi trả `422 Unprocessable Entity`).<br>• Chống trùng Name: Nếu đã tồn tại trả về `409 Conflict`.<br>• `SlugHelper.Generate(name)`: Bỏ dấu tiếng Việt, chữ thường, `-`. Nếu trùng slug thì tự động nối hậu tố `"-2"`, `"-3"`...<br>• Gọi `Category.Create(...)`, lưu vào DB.<br>• **Xóa cache**: `MemoryCache.Remove("categories:all")`.<br>• Status: `201 Created` kèm `Location: /api/v1/categories/{newSlug}`. |

---

## 3. Hướng Dẫn Biên Dịch Và Khởi Chạy (.NET 10 SDK)

### Yêu Cầu Môi Trường:
- [.NET 10 SDK](https://dotnet.microsoft.com/download) đã cài đặt trên máy.

### Lệnh Chạy Ứng Dụng:
```bash
# 1. Di chuyển vào thư mục WebApi
cd src/CulinaryBlog.WebApi

# 2. Khôi phục các gói NuGet dependencies
dotnet restore

# 3. Biên dịch và khởi chạy server API
dotnet run
```

Sau khi khởi chạy thành công:
- **Swagger UI**: `http://localhost:5000/swagger` hoặc `https://localhost:5001/swagger`
- **API Endpoint Danh mục**: `GET https://localhost:5001/api/v1/categories`
