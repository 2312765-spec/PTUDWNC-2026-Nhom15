/**
 * NFR-SEO-001 — chuỗi JSON-LD an toàn để nhúng vào `<script type="application/ld+json">`.
 *
 * `JSON.stringify` không escape `<`: tiêu đề/mô tả do người dùng nhập chứa `</script>` sẽ đóng thẻ
 * script sớm và chạy mã tùy ý trên trang (stored XSS). Escape `<`, `>`, `&` và U+2028/U+2029 thành
 * `\uXXXX` — vẫn là JSON hợp lệ, trình phân tích JSON-LD đọc ra đúng chuỗi gốc.
 */
export function serializeJsonLd(data: unknown): string {
  return JSON.stringify(data)
    .replace(/</g, '\\u003c')
    .replace(/>/g, '\\u003e')
    .replace(/&/g, '\\u0026')
    .replace(/\u2028/g, '\\u2028')
    .replace(/\u2029/g, '\\u2029');
}
