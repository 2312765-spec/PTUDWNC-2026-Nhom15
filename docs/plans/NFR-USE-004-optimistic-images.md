# Kế hoạch — NFR-USE-004: optimistic update + rollback cho ảnh recipe (mảng D)

**Ngày:** 2026-11-09 · **Branch:** `feature/NFR-USE-004-optimistic-images`
**Phạm vi:** chỉ frontend. Không có FR mới, không đổi backend. Quyết định áp dụng: **D22**, **D27**.
Tiếp nối `NFR-USE-004-optimistic-rollback.md` (mảng C) — mục "Còn thiếu" số 3.

## 1. Hiện trạng

| Thao tác (`lib/hooks/useRecipeImageGallery.ts`) | Hiện trạng |
|---|---|
| Đổi thứ tự (`reorder`) | ✅ optimistic + rollback (chỉ khôi phục `orderIndex`) |
| Đặt ảnh chính (`setPrimary`) | ❌ `await updateImage` rồi mới đổi state |
| Xoá ảnh (`remove`) | ❌ `await deleteImage` rồi mới đổi state; dialog loading chờ server |
| Sửa alt text | Chờ server (form sửa — cùng cách C làm) — ngoài phạm vi |

## 2. Việc làm

| Tầng | File | Thay đổi |
|---|---|---|
| Hook | `frontend/lib/hooks/useRecipeImageGallery.ts` | `setPrimary`: đổi `isPrimary` ngay rồi PATCH; lỗi → trả cờ primary cũ, **chỉ khi** primary hiện tại vẫn là ảnh đã đặt optimistic (không đè lần bấm sau đã thành công). `remove`: gỡ ảnh + chuyển primary (D22) ngay rồi DELETE; lỗi → chèn ảnh lại theo `orderIndex` + trả cờ primary cũ. Không ghi đè cả mảng (giữ ảnh upload xong / alt text sửa trong lúc chờ). Lỗi vẫn ném ra, toast lỗi từ `useRecipeImages` |
| Component | `frontend/components/upload/RecipeImageManager.tsx` | `confirmDelete` đóng dialog ngay; lỗi → ảnh hiện lại + toast "Xoá ảnh thất bại" |
| Unit | `frontend/__tests__/lib/hooks/useRecipeImageGallery.test.tsx` | State đổi trước khi promise resolve; rollback setPrimary; rollback xoá ảnh thường / ảnh primary; giữ ảnh upload xong trong lúc chờ |
| Unit | `frontend/__tests__/components/upload/RecipeImageManager.test.tsx` | Dialog đóng ngay; DELETE lỗi → ảnh hiện lại |
| E2E | `frontend/e2e/recipe-images.spec.ts` (mới) | `page.route` chặn DELETE/PATCH ảnh trả 500 → UI đổi ngay rồi quay về, toast lỗi, reload → server không đổi |
| Docs | `docs/traceability.md` | NFR-USE-004 🟡 → ✅ |

## 3. Đổi hành vi

Xoá lỗi: trước đây dialog giữ mở để thử lại; nay dialog đóng ngay, lỗi thì ảnh quay lại + toast,
người dùng bấm xoá lần nữa (giống xoá bước của C).

## 4. Phát sinh khi chạy E2E — lỗi upload (FR-RCP-008)

E2E upload thật bị server trả **415**: `apiClient` mặc định `Content-Type: application/json`, và với
header đó `transformRequest` của axios 1.7.9 biến `FormData` thành chuỗi JSON. Có từ commit 49f39a3
(2026-09-23); unit test cũ mock `apiClient.post` nên không bắt được. Sửa: POST ảnh đặt
`Content-Type: multipart/form-data` (adapter trình duyệt bỏ header để tự gắn boundary). Hồi quy:
`__tests__/lib/hooks/useRecipeImages.multipart.test.tsx` (apiClient thật, chỉ thay adapter). Commit riêng.

## 5. Commit

1. `fix(FR-RCP-008): upload ảnh gửi multipart thay vì JSON (415)`
2. `feat(NFR-USE-004): optimistic update + rollback khi xoá / đặt ảnh chính`
