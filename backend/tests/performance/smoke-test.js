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
  duration: '30s',
  thresholds: {
     // NFR-PERF-001: Chỉ áp ngưỡng p50 <= 150ms cho các endpoint có cache
    'http_req_duration{type:cached}': ['p(50)<=150', 'p(95)<=500', 'p(99)<=1000'],
    'http_req_failed': ['rate<0.01'],
  },
};


export default function () {
  // 1. Health check (không tính vào chỉ số cached)
  const healthRes = http.get(`${BASE_URL}/health`, { tags: { type: 'health' } });
  check(healthRes, {
    'health check status is 200': (r) => r.status === 200,
  });

  // 2. GET Categories (Cached - Tag: cached)
  const catRes = http.get(`${BASE_URL}/api/v1/categories`, { tags: { type: 'cached' } });
  check(catRes, {
    'categories status is 200': (r) => r.status === 200,
  });

  // 3. GET Recipes List (Cached - Tag: cached)
  const recRes = http.get(`${BASE_URL}/api/v1/recipes?page=1&pageSize=10`, { tags: { type: 'cached' } });
  check(recRes, {
    'recipes list status is 200': (r) => r.status === 200,
  });

  sleep(1);
}