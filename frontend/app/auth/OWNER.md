# app/auth — người phụ trách: **A**

`/auth/login` và `/auth/register` — CSR, redirect về `/dashboard` nếu đã đăng nhập.

Dùng React Hook Form + Zod. Hiển thị lỗi inline cạnh từng field, đọc từ
`ProblemDetails.errors` (NFR-USE-003).

D5: form đăng ký chỉ có `email`, `password`, `displayName`. **Không có** `fullName`, `userName`.
