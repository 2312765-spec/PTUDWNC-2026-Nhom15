# components/upload — người phụ trách: **D**

FR-RCP-008. NFR-USE-004: **progress bar realtime** (%) khi upload.

Validate phía client trước khi gửi (UX), nhưng **backend vẫn validate lại** —
client-side validation không phải là bảo mật:
- Kích thước <= 5MB
- MIME: image/jpeg, image/png, image/webp, image/avif
