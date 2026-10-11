/**
 * Chạy một lần trước toàn bộ E2E: backend phải sống, nếu không mọi test sẽ fail theo kiểu khó
 * hiểu (trang chủ chỉ hiện "Không tải được…"). Báo lỗi rõ cách bật thay vì để test đỏ hàng loạt.
 */
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1';

export default async function globalSetup() {
  try {
    const res = await fetch(`${API_BASE_URL}/categories`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
  } catch (error) {
    throw new Error(
      `E2E cần backend đang chạy tại ${API_BASE_URL} (${String(error)}).\n` +
        'Bật: docker compose up -d && dotnet run --project backend/CulinaryBlog.API',
    );
  }
}
