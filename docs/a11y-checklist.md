# Checklist kiểm tra tay — NFR-USE-002 (WCAG 2.1 AA)

> Theo **D83**: axe-core trong `frontend/e2e/a11y.spec.ts` lo phần tự động (tương phản chữ, ARIA, label, cấu trúc).
> File này là phần axe **không** kiểm được. Screen reader chỉ thử **NVDA** — VoiceOver ngoài phạm vi v1.
>
> NFR-USE-002 chỉ ✅ khi `a11y.spec.ts` xanh **và** mọi mục dưới đây đã đánh dấu. Ghi ngày + người chạy ở cuối file.

## Chuẩn bị

- Chạy hệ thống: `docker compose up -d` · `dotnet run --project backend/CulinaryBlog.API` · `cd frontend && npm run dev`.
- Tài khoản dev: `author1@seed.culinaryblog.local` / `admin@seed.culinaryblog.local`, mật khẩu `Password123!`.
- NVDA (miễn phí, nvaccess.org) + Chrome hoặc Firefox. Phím NVDA mặc định: `Insert`.
  - `NVDA + Space`: chuyển chế độ duyệt ↔ tương tác · `H`: nhảy heading · `D`: nhảy landmark · `F`: nhảy ô nhập ·
    `NVDA + F7`: danh sách link/heading/landmark.
- Kiểm bàn phím: **rút chuột**, chỉ dùng `Tab`, `Shift+Tab`, `Enter`, `Space`, `Esc`, phím mũi tên.

## A. Bàn phím (WCAG 2.1.1, 2.1.2, 2.4.3, 2.4.7)

Với mỗi luồng: mọi thao tác làm được, thứ tự Tab hợp lý (trái → phải, trên → dưới), **luôn thấy viền focus**, không
bị kẹt focus.

- [x] Mọi trang: `Tab` đầu tiên hiện link **"Bỏ qua, tới nội dung chính"**, `Enter` → focus nhảy vào nội dung.
- [x] Header: tới được Công thức / Danh mục / Tìm kiếm / Đăng nhập / Đăng ký (hoặc Bảng điều khiển / Hồ sơ / Đăng xuất).
- [x] Đăng ký → Đăng nhập: điền form, nút hiện/ẩn mật khẩu dùng được bằng `Space`, gửi bằng `Enter`.
- [x] Trang chủ → ô tìm kiếm → `Enter` → `/search` → mở một kết quả.
- [ ] `/recipes`: đổi bộ lọc (danh mục, độ khó, sắp xếp), chuyển trang bằng thanh phân trang.
- [x] `/dashboard/recipes`: tab trạng thái — `←`/`→`/`Home`/`End` đổi tab, `Tab` vào danh sách.
- [ ] Wizard `/dashboard/recipes/new`: đi hết 4 bước, thêm/xóa nguyên liệu và bước, tạo công thức.
- [x] Trang sửa: đổi chỗ bước bằng nút **Lên/Xuống** (thay kéo-thả), focus vẫn ở nút vừa bấm hoặc bước vừa chuyển.
- [x] Trang sửa: Xuất bản / Lưu trữ / Xóa — hộp xác nhận mở ra thì focus nằm **trong** hộp, `Tab` không thoát ra
      ngoài, `Esc` đóng và focus **trở về** nút đã mở.
- [ ] Ảnh (D): chọn ảnh qua nút tải lên, đặt ảnh chính, sửa alt text (`Enter` lưu, `Esc` hủy), xóa ảnh.
- [ ] `/dashboard/categories` (Admin): thêm / sửa / xóa danh mục, hộp xác nhận như trên.
- [x] Toast sau khi lưu: nút đóng `×` tới được bằng `Tab`, toast không tự cướp focus.

## B. NVDA (WCAG 1.3.1, 4.1.2, 4.1.3)

- [x] `D` (landmark): nghe được banner/header, navigation, main, contentinfo/footer.
- [x] `H` (heading): mỗi trang đúng **một** `h1`, các cấp không nhảy cóc (h1 → h2 → h3).
- [x] Form đăng ký: mỗi ô đọc đúng nhãn + gợi ý; gửi form rỗng → NVDA **đọc lỗi** của ô đang focus.
- [x] Danh sách yêu cầu mật khẩu: trạng thái đạt/chưa đạt được đọc ra (không chỉ dựa vào icon ✓/✗).
- [ ] Toast thành công/lỗi được đọc tự động khi xuất hiện (không cần di chuyển focus tới).
- [ ] Skeleton đang tải: NVDA đọc "Đang tải…" chứ không đọc chuỗi khối rỗng.
- [ ] `/dashboard`: 4 ô số đọc thành "Nháp 3, link" …; danh sách "Công thức gần đây" đọc tiêu đề + trạng thái + ngày.
- [ ] Trang chi tiết recipe: ảnh có alt text có nghĩa (hoặc ẩn nếu chỉ trang trí); số bước đọc theo thứ tự.
- [x] Nút chỉ có icon (sửa, xóa, Lên/Xuống, đóng toast, hiện mật khẩu) đều đọc ra tên hành động.
- [x] Tab trạng thái `/dashboard/recipes`: đọc "tab, đã chọn, 1 trên 4".

## C. Tương phản thành phần UI & phóng to (WCAG 1.4.4, 1.4.10, 1.4.11)

axe không đo viền và icon. Đo bằng DevTools (Inspect → color picker hiện contrast) hoặc WebAIM Contrast Checker.

- [x] Viền ô nhập / select / textarea ≥ 3:1 so với nền (token `border-strong` #78716c ≈ 4.8:1).
- [x] Viền focus ≥ 3:1 trên mọi nền nó xuất hiện (trắng, xám nhạt, kem trang chủ).
- [ ] Icon mang nghĩa (cảnh báo lỗi, ✓/✗ mật khẩu, badge trạng thái) ≥ 3:1.
- [ ] Ô sửa alt text của ảnh (`RecipeImageCard`, có `outline-none`): viền `brand-600` vẫn thấy rõ khi đang sửa.
- [x] Phóng to trình duyệt **200%**: không mất chữ, không chồng chéo.
- [x] Cửa sổ rộng **320px**: không cuộn ngang (trừ bảng dữ liệu nếu có).

## Kết quả

Mục `[x]` đã kiểm **tự động** bằng Playwright (Chromium) — đi bàn phím thật, đọc cây accessibility mà NVDA dùng, đo
màu đã render. Mục `[ ]` cần người: nghe NVDA đọc thật, hoặc luồng chưa tự động hóa.

| Ngày | Người chạy | Trình duyệt + NVDA | Mục chưa đạt (ghi issue) |
|---|---|---|---|
| 2026-11-08 | Claude (tự động, Playwright) | Chromium, không NVDA | Đã sửa trong cùng nhánh: (1) đổi chỗ bước/nguyên liệu bằng nút Lên/Xuống làm focus rơi về `<body>` — nay giữ focus trên mục vừa chuyển (`e2e/a11y.spec.ts`); (2) `/recipes`, `/search` heading h1 → h3 — thêm h2 ẩn; (3) toast thành công tạo live region cùng lúc với nội dung (NVDA dễ bỏ qua) — nay chèn vào vùng `aria-live` có sẵn; (4) toast thành công: mô tả 4.39:1, nút đóng 2.88:1 — bỏ/giảm `opacity`. Còn lại: các mục `[ ]` |
| | | | |
