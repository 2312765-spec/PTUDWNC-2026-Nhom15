# Kế hoạch — NFR-USE-004: E2E optimistic update + rollback (mảng C)

**Ngày:** 2026-11-07 · **Branch:** `feature/NFR-USE-004-optimistic-rollback-e2e`
**Phạm vi:** chỉ frontend E2E + tài liệu. Không có FR mới, không đổi backend.

## 1. Hiện trạng (đã có trong code + unit test)

| Thao tác | Optimistic + rollback | Toast | Unit test |
|---|---|---|---|
| Xóa / đổi chỗ bước (FR-RCP-010) | ✅ `RecipeStepsManager.tsx` (`run(action, rollback)`) | ✅ | ✅ `RecipeStepsManager.test.tsx` |
| Xóa / đổi chỗ nguyên liệu (FR-RCP-009) | ✅ `RecipeIngredientsManager.tsx` | ✅ | ✅ `RecipeIngredientsManager.test.tsx` |
| Thêm / sửa bước, nguyên liệu; wizard; sửa thông tin chung | Chờ server (cần `id` / `rowVersion` mới) | ✅ | ✅ |
| Publish / archive / delete (FR-RCP-005/006/007) | Chờ server + confirm dialog (D3 kiểm ở server, ETag mới — optimistic dễ nhấp nháy sai) | ✅ | ✅ |

## 2. Còn thiếu

1. Không có E2E nào kiểm tra rollback khi API lỗi thật trên trình duyệt.
2. Dòng NFR-USE-004 trong `traceability.md` ghi sai ("còn thiếu optimistic + rollback, toast").
3. Ảnh recipe (`lib/hooks/useRecipeImages.ts` — xóa, đặt ảnh chính) chưa optimistic — **thuộc D**, ngoài phạm vi kế hoạch này, chỉ ghi chú lại.

## 3. Việc làm

| Tầng | File | Thay đổi |
|---|---|---|
| E2E | `frontend/e2e/recipe-editor.spec.ts` | +1 test: `page.route` chặn `PUT …/steps/{id}` và `DELETE …/steps/{id}` trả 500 → thứ tự / bước bị xóa quay về như cũ, toast lỗi "Không lưu được thay đổi"; tải lại trang → server không đổi |
| Docs | `docs/traceability.md` | Cập nhật dòng NFR-USE-004 theo bảng mục 1, giữ 🟡 vì phần ảnh của D |

## 4. Commit

`test(NFR-USE-004): E2E hoàn tác khi đổi chỗ / xóa bước lỗi`
