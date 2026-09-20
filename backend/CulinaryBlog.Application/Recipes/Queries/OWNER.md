# Application/Recipes/Queries — người phụ trách: **B**

FR-RCP-001, FR-RCP-002, FR-SRCH-001 → 004. Slice S4 (tuần 4–5), S10 (tuần 6–7).

**Authorization filter của `GetRecipesQuery` là chỗ nguy hiểm nhất cả dự án:**

```
Guest   → WHERE Status = Published
Author  → WHERE Status = Published OR (Status IN (Draft, Archived) AND AuthorId = currentUserId)
Admin   → không filter
```

Danh sách **lọc** (không 403). Truy cập trực tiếp theo slug thì **403** (FR-RCP-002 A2).

**Quyết định bắt buộc:** D8, D14 (có `minServings`), D10 (route `/recipes/search` đăng ký
TRƯỚC `/recipes/{slug}`).
