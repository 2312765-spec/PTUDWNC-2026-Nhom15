# IntegrationTests/Recipes — người phụ trách: **B** (queries) + **C** (commands) + **D** (images)

**Ba test dễ quên nhất — đừng bỏ:**
1. Danh sách của Guest **không chứa** Draft của bất kỳ ai.
2. Author A **không thấy** Draft của Author B trong danh sách, và nhận **403** khi truy cập trực tiếp theo slug.
3. Admin xóa/sửa được recipe của Author khác (bypass ownership).

Seed data phải có **cả Draft và Archived** — nếu toàn Published thì bug phân quyền không lộ ra.
