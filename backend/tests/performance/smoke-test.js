import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
/*
Kiểm tra tính ổn định ban đầu với tải nhẹ (1–5 VUs) 
và thẩm định các ngưỡng: p50 ≤ 150ms, p95 ≤ 500ms, 
p99 ≤ 1000ms (đáp ứng NFR-PERF-001)
*/
export const options = {
  // Kịch bản Smoke: 5 VU trong vòng 1 phút
  vus: 5,
  duration: '1m',
  thresholds: {
    // NFR-PERF-001: p50 <= 150ms, p95 <= 500ms, p99 <= 1000ms
    http_req_duration: [
      'p(50)<=150',  // 50% request hoàn thành trong 150ms
      'p(95)<=500',  // 95% request hoàn thành trong 500ms
      'p(99)<=1000', // 99% request hoàn thành trong 1000ms
    ],
    http_req_failed: ['rate<0.01'], // Tỉ lệ lỗi < 1%
  },
};

export default function () {
  // 1. Kiểm tra Health check
  const resHealth = http.get(`${BASE_URL}/health`);
  check(resHealth, {
    'health check status is 200': (r) => r.status === 200,
  });

  // 2. Lấy danh sách danh mục (FR-CAT-001, có gắn cache)
  const resCats = http.get(`${BASE_URL}/api/v1/categories`);
  check(resCats, {
    'categories status is 200': (r) => r.status === 200,
    'categories has data': (r) => r.body && r.body.length > 0,
  });

  // 3. Lấy danh sách công thức trang chủ (FR-RCP-001, có phân trang & cache)
  const resRecipes = http.get(`${BASE_URL}/api/v1/recipes?page=1&pageSize=10`);
  check(resRecipes, {
    'recipes list status is 200': (r) => r.status === 200,
  });

  // 4. Tìm kiếm công thức (FR-SRCH-001)
  const resSearch = http.get(`${BASE_URL}/api/v1/recipes/search?q=pho&page=1&pageSize=10`);
  check(resSearch, {
    'search status is 200': (r) => r.status === 200,
  });

  sleep(1);
}