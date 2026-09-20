# IntegrationTests/Categories — người phụ trách: **B**

4 test phân quyền bắt buộc cho mỗi endpoint Admin (xem `docs/permissions.md` mục 6):
không token → 401 · Author → 403 · Admin → 2xx · Admin thao tác trên tài nguyên người khác → 2xx.

Thêm: Name trùng → 409 · slug tiếng Việt đúng · xóa category còn recipe → 409 ·
**soft delete: bản ghi vẫn còn trong DB** (D2) · sửa tên category → `GET /recipes` trả tên mới (cache invalidate).
